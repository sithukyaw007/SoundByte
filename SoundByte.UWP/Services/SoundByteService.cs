﻿/* |----------------------------------------------------------------|
 * | Copyright (c) 2017, Grid Entertainment                         |
 * | All Rights Reserved                                            |
 * |                                                                |
 * | This source code is to only be used for educational            |
 * | purposes. Distribution of SoundByte source code in             |
 * | any form outside this repository is forbidden. If you          |
 * | would like to contribute to the SoundByte source code, you     |
 * | are welcome.                                                   |
 * |----------------------------------------------------------------|
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Security.Credentials;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;
using Newtonsoft.Json;
using SoundByte.Core;
using SoundByte.UWP.Helpers;
using SoundByte.Core.Exceptions;
using SoundByte.Core.Items;
using SoundByte.Core.Items.User;
using SoundByte.Core.Services;

namespace SoundByte.UWP.Services
{
    public class SoundByteService
    {
        #region Disconnect Service Helpers

        /// <summary>
        ///     Disconnects a soundbyte service
        /// </summary>
        public void DisconnectService(ServiceType serviceType)
        {
            // Gen3.0 Service
            SoundByteV3Service.Current.DisconnectService(serviceType);

            switch (serviceType)
            {
                case ServiceType.SoundCloud:
                    // Remove the token
                    _soundCloudToken = null;
                    break;
                case ServiceType.Fanburst:
                    // Remove the token
                    _fanburstToken = null;
                    break;
            }
        }

        #endregion

        #region Static Class Instance

        // Private class instance
        private static readonly Lazy<SoundByteService> InstanceHolder =
            new Lazy<SoundByteService>(() => new SoundByteService());

        /// <summary>
        ///     Get the current soundbyte service
        /// </summary>
        public static SoundByteService Instance => InstanceHolder.Value;
        #endregion

        private SoundByteService()
        {
            // This is all temp, this service is acting as a wrapper for the new
            // Gen3.0 service.
            var secretList = new List<ServiceSecret>
            {
                new ServiceSecret
                {
                    Service = ServiceType.SoundCloud,
                    ClientId = ApiKeyService.SoundCloudClientId,
                    ClientSecret = ApiKeyService.SoundCloudClientSecret,
                    UserToken = SoundCloudToken
                },
                new ServiceSecret
                {
                    Service = ServiceType.SoundCloudV2,
                    ClientId = ApiKeyService.SoundCloudClientId,
                    ClientSecret = ApiKeyService.SoundCloudClientSecret,
                    UserToken = SoundCloudToken
                },
                new ServiceSecret
                {
                    Service = ServiceType.Fanburst,
                    ClientId = ApiKeyService.FanburstClientId,
                    ClientSecret = ApiKeyService.FanburstClientSecret,
                    UserToken = FanburstToken
                },
                new ServiceSecret
                {
                    Service = ServiceType.YouTube,
                    ClientId = ApiKeyService.YouTubeClientId
                }
            };

            SoundByteV3Service.Current.Init(secretList);
        }

        #region Secret Keys

        private LoginToken _soundCloudToken;
        private LoginToken _fanburstToken;
        private BaseUser _currentSoundCloudUser;
        private BaseUser _currentFanBurstUser;

        #endregion

        #region Getters and Setters

        /// <summary>
        ///     Get the token needed to access fanburst resources
        /// </summary>
        private LoginToken FanburstToken
        {
            get
            {
                // If we already have the token, return it
                if (_fanburstToken != null)
                    return _fanburstToken;

                // Perform a check to see if we are logged in
                if (!SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst))
                    return null;

                // Get the password vault
                var vault = new PasswordVault();

                try
                {
                    // Get soundcloud recources
                    var soundCloudResource = vault.FindAllByResource("SoundByte.FanBurst").FirstOrDefault();

                    // If this resource does not exist, return false
                    if (soundCloudResource == null)
                        return null;

                    // Get the soundcloud vault items
                    var token = vault.Retrieve("SoundByte.FanBurst", "Token").Password;

                    // Create a new token class
                    var tokenHolder = new LoginToken
                    {
                        AccessToken = token
                    };

                    // Set the private token
                    _fanburstToken = tokenHolder;

                    // Return the newly created token
                    return tokenHolder;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        ///     Get the token needed to access soundcloud resources
        /// </summary>
        private LoginToken SoundCloudToken
        {
            get
            {
                // If we already have the token, return it
                if (_soundCloudToken != null)
                    return _soundCloudToken;

                // Perform a check to see if we are logged in
                if (!SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                    return null;

                // Get the password vault
                var vault = new PasswordVault();

                try
                {
                    // Get soundcloud recources
                    var soundCloudResource = vault.FindAllByResource("SoundByte.SoundCloud").FirstOrDefault();

                    // If this resource does not exist, return false
                    if (soundCloudResource == null)
                        return null;

                    // Get the soundcloud vault items
                    var token = vault.Retrieve("SoundByte.SoundCloud", "Token").Password;
                    var scope = vault.Retrieve("SoundByte.SoundCloud", "Scope").Password;

                    // Create a new token class
                    var tokenHolder = new LoginToken
                    {
                        AccessToken = token,
                        Scope = scope
                    };

                    // Set the private token
                    _soundCloudToken = tokenHolder;

                    // Return the newly created token
                    return tokenHolder;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        ///     The current logged in fanburst user
        /// </summary>
        public BaseUser FanburstUser
        {
            get
            {
                // Handle account disconnect
                if (!SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst))
                {
                    _currentFanBurstUser = null;
                    return null;
                }

                // If we have a user object, return it
                if (_currentFanBurstUser != null)
                    return _currentFanBurstUser;

                try
                {
                    _currentFanBurstUser =
                        AsyncHelper.RunSync(async () => await GetAsync<FanburstUser>(ServiceType.Fanburst, "/me")).ToBaseUser();
                    return _currentFanBurstUser;
                }
                catch
                {
                    DisconnectService(ServiceType.Fanburst);
                    return null;
                }
            }
        }

        /// <summary>
        ///     The current logged in soundcloud user
        /// </summary>
        public BaseUser SoundCloudUser
        {
            get
            {
                // Handle account disconnect
                if (!SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                {
                    _currentSoundCloudUser = null;
                    return null;
                }

                // If we have a user object, return it
                if (_currentSoundCloudUser != null)
                    return _currentSoundCloudUser;

                try
                {
                    _currentSoundCloudUser = AsyncHelper.RunSync(async () => await GetAsync<SoundCloudUser>(ServiceType.SoundCloud, "/me")).ToBaseUser();
                    return _currentSoundCloudUser;
                }
                catch
                {
                    DisconnectService(ServiceType.SoundCloud);
                    return null;
                }
            }
        }

        #endregion

        #region Account Connected Checks
        /// <summary>
        ///     Checks to see if a users Fanburst account is connected. This method currently wraps
        ///     SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst);
        /// </summary>
        [Obsolete("Use SoundByteV3Service.Current.IsServiceConnected", true)]
        public bool IsFanBurstAccountConnected => SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst);

        /// <summary>
        ///     Checks to see if a users SoundCloud account is connected. This method currently wraps
        ///     SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud);
        /// </summary>
        [Obsolete ("Use SoundByteV3Service.Current.IsServiceConnected", true)]
        public bool IsSoundCloudAccountConnected => SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud);
        #endregion

        #region WebAPI Helpers

        /// <summary>
        ///     Contacts the soundcloud API and posts an object. The posted object will then be
        ///     returned back to the user.
        /// </summary>
        /// <typeparam name="T">The object type we will serialize</typeparam>
        /// <param name="endpoint"></param>
        /// <param name="bodyContent">The content we want to post</param>
        /// <param name="optionalParams">A list of any optional params to send in the URI</param>
        /// <param name="useV2Api">If true, the v2 version of the soundcloud API will be contacted instead.</param>
        /// <returns></returns>
        public async Task<T> PostAsync<T>(string endpoint, HttpStringContent bodyContent = null,
            Dictionary<string, string> optionalParams = null, bool useV2Api = false)
        {
            // Strip out the / infront of the endpoint if it exists
            endpoint = endpoint.TrimStart('/');

            // Start building the request URL
            var requestUri = useV2Api
                ? $"https://api-v2.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}"
                : $"https://api.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";

            // Check that there are optional params then loop through all 
            // the params and add them onto the request URL
            if (optionalParams != null)
                requestUri = optionalParams
                    .Where(param => !string.IsNullOrEmpty(param.Key) && !string.IsNullOrEmpty(param.Value))
                    .Aggregate(requestUri, (current, param) => current + "&" + param.Key + "=" + param.Value);

            // Get the resource loader
            var resources = ResourceLoader.GetForViewIndependentUse();

            try
            {
                return await Task.Run(async () =>
                {
                    // Create the client
                    using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                    {
                        // Setup the request readers for user agent
                        // and requested data type.
                        client.DefaultRequestHeaders.UserAgent.Add(new HttpProductInfoHeaderValue("SoundByte",
                            Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor + "." +
                            Package.Current.Id.Version.Build));
                        client.DefaultRequestHeaders.Accept.Add(
                            new HttpMediaTypeWithQualityHeaderValue("application/json"));

                        // Auth headers for when the user is logged in
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                            client.DefaultRequestHeaders.Authorization =
                                new HttpCredentialsHeaderValue("OAuth", SoundCloudToken.AccessToken);

                        // escape the url
                        var escapedUri = new Uri(Uri.EscapeUriString(requestUri));

                        // Full the body content if it is null
                        if (bodyContent == null)
                            bodyContent = new HttpStringContent(string.Empty);

                        // Get the URL
                        using (var webRequest = await client.PostAsync(escapedUri, bodyContent))
                        {
                            // Throw exception if the request failed
                            if (webRequest.StatusCode != HttpStatusCode.Ok)
                                throw new SoundByteException("Connection Error", webRequest.ReasonPhrase, "\uEB63");

                            // Get the body of the request as a stream
                            using (var webStream = await webRequest.Content.ReadAsInputStreamAsync())
                            {
                                // Read the stream
                                using (var streamReader = new StreamReader(webStream.AsStreamForRead()))
                                {
                                    // Get the text from the stream
                                    using (var textReader = new JsonTextReader(streamReader))
                                    {
                                        // Used to get the data from JSON
                                        var serializer =
                                            new JsonSerializer {NullValueHandling = NullValueHandling.Ignore};
                                        // Return the data
                                        return serializer.Deserialize<T>(textReader);
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (TaskCanceledException)
            {
                throw new SoundByteException(resources.GetString("HttpError_Header"),
                    resources.GetString("HttpError_TaskCancel"), "\uE007");
            }
            catch (JsonSerializationException)
            {
                throw new SoundByteException(resources.GetString("HttpError_Header"),
                    resources.GetString("HttpError_JsonError"), "\uEB63");
            }
            catch (Exception ex)
            {
                throw new SoundByteException(resources.GetString("GeneralError_Header"),
                    string.Format(resources.GetString("GeneralError_Content"), ex.Message), "\uE007");
            }
        }

        public async Task<string> GridEntertainmentSoundByteGetPlaybackKey()
        {
            try
            {
                using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                {
                    var key = await client.GetStringAsync(
                        new Uri("https://gridentertainment.net/api/soundbyte/playback-key"));
                    key = key.Trim('"');
                    return key;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> ApiCheck(string url)
        {
            try
            {
                // Create the client
                using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                {
                    // No Auth for this
                    client.DefaultRequestHeaders.Authorization = null;

                    using (var webRequest = await client.GetAsync(new Uri(Uri.EscapeUriString(url))))
                    {
                        return webRequest.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Fetches an object from the soundcloud api and returns it back
        ///     to the user. Note: This class will return a SoundByteException
        ///     if anything goes wrong. Make sure to handle this exception.
        /// </summary>
        /// <typeparam name="T">The object type we will serialize</typeparam>
        /// <param name="service">The service that we want to connect to</param>
        /// <param name="endpoint">The service endpoint that we want</param>
        /// <param name="optionalParams">A list of any optional params to send in the URI</param>
        /// <returns></returns>
        public async Task<T> GetAsync<T>(ServiceType service, string endpoint,
            Dictionary<string, string> optionalParams = null)
        {
            // Strip out the / infront of the endpoint if it exists
            endpoint = endpoint.TrimStart('/');

            // Start building the request URL
            var requestUri = string.Empty;

            switch (service)
            {
                case ServiceType.SoundCloud:
                    requestUri = 
                        $"https://api.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";
                    break;

                case ServiceType.SoundCloudV2:
                    requestUri = 
                        $"https://api-v2.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";
                    break;

                case ServiceType.Fanburst:
                    requestUri =
                        $"https://api.fanburst.com/{endpoint}?client_id={ApiKeyService.FanburstClientId}&client_secret={ApiKeyService.FanburstClientSecret}";
                    break;
                case ServiceType.YouTube:
                    requestUri =
                        $"https://www.googleapis.com/youtube/v3/{endpoint}?key={ApiKeyService.YouTubeClientId}";
                    break;
            }

            // Check that there are optional params then loop through all 
            // the params and add them onto the request URL
            if (optionalParams != null)
                requestUri = optionalParams
                    .Where(param => !string.IsNullOrEmpty(param.Key) && !string.IsNullOrEmpty(param.Value))
                    .Aggregate(requestUri, (current, param) => current + "&" + param.Key + "=" + param.Value);

            // Get the resource loader
            var resources = ResourceLoader.GetForViewIndependentUse();

            try
            {
                return await Task.Run(async () =>
                {
                    // Create the client
                    using (var client = new HttpClient(new HttpBaseProtocolFilter { AutomaticDecompression = true }))
                    {
                        // Setup the request readers for user agent
                        // and requested data type.
                        client.DefaultRequestHeaders.UserAgent.Add(new HttpProductInfoHeaderValue("SoundByte",
                            Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor + "." +
                            Package.Current.Id.Version.Build));
                        client.DefaultRequestHeaders.Accept.Add(
                            new HttpMediaTypeWithQualityHeaderValue("application/json"));

                        // Auth headers for when the user is logged in
                        // Different for each service
                        switch (service)
                        {
                            case ServiceType.SoundCloud:
                            case ServiceType.SoundCloudV2:
                                if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                                    client.DefaultRequestHeaders.Authorization =
                                        new HttpCredentialsHeaderValue("OAuth", SoundCloudToken.AccessToken);
                                break;

                            case ServiceType.Fanburst:
                                if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.Fanburst))
                                    client.DefaultRequestHeaders.Authorization =
                                        new HttpCredentialsHeaderValue("OAuth", FanburstToken.AccessToken);
                                break;
                        }

                        // escape the url
                        var escapedUri = new Uri(Uri.EscapeUriString(requestUri));


                        // Get the URL
                        using (var webRequest = await client.GetAsync(escapedUri))
                        {
                            // Throw exception if the request failed
                            if (webRequest.StatusCode != HttpStatusCode.Ok)
                                throw new SoundByteException("Connection Error", webRequest.ReasonPhrase, "\uEB63");

                            // Get the body of the request as a stream
                            using (var webStream = await webRequest.Content.ReadAsInputStreamAsync())
                            {
                                // Read the stream
                                using (var streamReader = new StreamReader(webStream.AsStreamForRead()))
                                {
                                    // Get the text from the stream
                                    using (var textReader = new JsonTextReader(streamReader))
                                    {
                                        // Used to get the data from JSON
                                        var serializer =
                                            new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
                                        // Return the data
                                        return serializer.Deserialize<T>(textReader);
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (OperationCanceledException)
            {
                return default(T);
            }
            catch (JsonSerializationException)
            {
                throw new SoundByteException(resources.GetString("HttpError_Header"),
                    resources.GetString("HttpError_JsonError"), "\uEB63");
            }
            catch (Exception ex)
            {
                throw new SoundByteException(resources.GetString("GeneralError_Header"),
                    string.Format(resources.GetString("GeneralError_Content"), ex.Message), "\uE007");
            }
        }

        /// <summary>
        /// A wrapper function 
        /// </summary>
        /// <typeparam name="T">The object type we will serialize</typeparam>
        /// <param name="endpoint">SoundClouud API endpoint to contact</param>
        /// <param name="optionalParams">A list of any optional params to send in the URI</param>
        /// <param name="useV2Api">If true, the v2 version of the soundcloud API will be contacted instead.</param>
        /// <param name="service">The service that we want to connect to</param>
        /// <returns>The object you specified</returns>
        [Obsolete("This method is obsolete. Use GetAsync<T>(service, endpoint, optionalParams) instead.")]
        public async Task<T> GetAsync<T>(string endpoint, Dictionary<string, string> optionalParams = null,
            bool useV2Api = false, ServiceType service = ServiceType.SoundCloud)
        {
            if (service == ServiceType.SoundCloud && useV2Api)
                service = ServiceType.SoundCloudV2;

            return await GetAsync<T>(service, endpoint, optionalParams);
        }

        /// <summary>
        ///     Attempts to delete an object from the soundcloud
        ///     api and returns the result.
        /// </summary>
        /// <param name="endpoint">The endpoint to delete from</param>
        /// <param name="useV2Api">Should we try deleting from the v2 api</param>
        /// <returns>If the delete was successful</returns>
        public async Task<bool> DeleteAsync(string endpoint, bool useV2Api = false)
        {
            // Strip out the / infront of the endpoint if it exists
            endpoint = endpoint.TrimStart('/');

            // Start building the request URL
            var requestUri = useV2Api
                ? $"https://api-v2.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}"
                : $"https://api.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";

            try
            {
                return await Task.Run(async () =>
                {
                    // Create the client
                    using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                    {
                        // Setup the request readers for user agent
                        // and requested data type.
                        client.DefaultRequestHeaders.UserAgent.Add(new HttpProductInfoHeaderValue("SoundByte",
                            Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor + "." +
                            Package.Current.Id.Version.Build));
                        client.DefaultRequestHeaders.Accept.Add(
                            new HttpMediaTypeWithQualityHeaderValue("application/json"));

                        // Auth headers for when the user is logged in
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                            client.DefaultRequestHeaders.Authorization =
                                new HttpCredentialsHeaderValue("OAuth", SoundCloudToken.AccessToken);

                        // escape the url
                        var escapedUri = new Uri(Uri.EscapeUriString(requestUri));

                        // Get the URL
                        using (var webRequest = await client.DeleteAsync(escapedUri))
                        {
                            // Return if successful
                            return webRequest.StatusCode == HttpStatusCode.Ok;
                        }
                    }
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Checks to see if an items exists at the specified endpoint
        /// </summary>
        /// <param name="endpoint">The endpoint we are checking</param>
        /// <param name="useV2Api">Should we try checking the v2 api</param>
        /// <returns>If the object exists</returns>
        public async Task<bool> ExistsAsync(string endpoint, bool useV2Api = false)
        {
            // Strip out the / infront of the endpoint if it exists
            endpoint = endpoint.TrimStart('/');

            // Start building the request URL
            var requestUri = useV2Api
                ? $"https://api-v2.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}"
                : $"https://api.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";

            try
            {
                return await Task.Run(async () =>
                {
                    // Create the client
                    using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                    {
                        // Setup the request readers for user agent
                        // and requested data type.
                        client.DefaultRequestHeaders.UserAgent.Add(new HttpProductInfoHeaderValue("SoundByte",
                            Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor + "." +
                            Package.Current.Id.Version.Build));
                        client.DefaultRequestHeaders.Accept.Add(
                            new HttpMediaTypeWithQualityHeaderValue("application/json"));

                        // Auth headers for when the user is logged in
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new HttpCredentialsHeaderValue("OAuth", SoundCloudToken.AccessToken);
                            requestUri += "&oauth_token=" + SoundCloudToken.AccessToken;
                        }

                        // escape the url
                        var escapedUri = new Uri(Uri.EscapeUriString(requestUri));

                        // Get the URL
                        using (var webRequest = await client.GetAsync(escapedUri))
                        {
                            // lol, why soundcloud. why do I have to do this??
                            if (webRequest.StatusCode == HttpStatusCode.Unauthorized)
                                return true;

                            // Return if the resource exists
                            return webRequest.IsSuccessStatusCode;
                        }
                    }
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Puts an object into the soundcloud API.
        /// </summary>
        /// <param name="endpoint">The endpoint we are putting the item into</param>
        /// <param name="bodyContent">The content we want to put</param>
        /// <param name="useV2Api">Should we try putting to the v2 api</param>
        /// <returns>If the task was successful</returns>
        public async Task<bool> PutAsync(string endpoint, HttpStringContent bodyContent = null, bool useV2Api = false)
        {
            // Strip out the / infront of the endpoint if it exists
            endpoint = endpoint.TrimStart('/');

            // Start building the request URL
            var requestUri = useV2Api
                ? $"https://api-v2.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}"
                : $"https://api.soundcloud.com/{endpoint}?client_id={ApiKeyService.SoundCloudClientId}&client_secret={ApiKeyService.SoundCloudClientSecret}";

            try
            {
                return await Task.Run(async () =>
                {
                    // Create the client
                    using (var client = new HttpClient(new HttpBaseProtocolFilter {AutomaticDecompression = true}))
                    {
                        // Setup the request readers for user agent
                        // and requested data type.
                        client.DefaultRequestHeaders.UserAgent.Add(new HttpProductInfoHeaderValue("SoundByte SBB",
                            Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor + "." +
                            Package.Current.Id.Version.Build));
                        client.DefaultRequestHeaders.Accept.Add(
                            new HttpMediaTypeWithQualityHeaderValue("application/json"));

                        // Auth headers for when the user is logged in
                        if (SoundByteV3Service.Current.IsServiceConnected(ServiceType.SoundCloud))
                            client.DefaultRequestHeaders.Authorization =
                                new HttpCredentialsHeaderValue("OAuth", SoundCloudToken.AccessToken);

                        // escape the url
                        var escapedUri = new Uri(Uri.EscapeUriString(requestUri));

                        // Full the body content if it is null
                        if (bodyContent == null)
                            bodyContent = new HttpStringContent(string.Empty);

                        // Get the URL
                        using (var webRequest = await client.PutAsync(escapedUri, bodyContent))
                        {
                            // Return if tsuccessful
                            return webRequest.IsSuccessStatusCode;
                        }
                    }
                });
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}