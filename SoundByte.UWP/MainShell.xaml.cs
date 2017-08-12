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
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.VoiceCommands;
using Windows.Globalization;
using Windows.Services.Store;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.Services.Store.Engagement;
using Microsoft.Toolkit.Uwp;
using NotificationsExtensions;
using NotificationsExtensions.Toasts;
using SoundByte.Core.API.Endpoints;
using SoundByte.Core.Dialogs;
using SoundByte.Core.Helpers;
using SoundByte.Core.Services;
using SoundByte.UWP.Models;
using SoundByte.UWP.Services;
using SoundByte.UWP.Views;
using SoundByte.UWP.Views.Application;
using SoundByte.UWP.Views.CoreApp;
using SoundByte.UWP.Views.Me;
using UICompositionAnimations.Brushes;
using Playlist = SoundByte.Core.API.Endpoints.Playlist;
using SearchBox = SoundByte.UWP.UserControls.SearchBox;

namespace SoundByte.UWP
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainShell
    {
        public MainShell(string path)
        {
            // Init the XAML
            InitializeComponent();

            // Set the accent color
            TitlebarHelper.UpdateTitlebarStyle();

            // When the page is loaded (after the following and xaml init)
            // we can perform the async work
            Loaded += async (sender, args) => await PerformAsyncWork(path);

            // This is a dirty to show the now playing
            // bar when a track is played. This method
            // updates the required layout for the now
            // playing bar.
            Service.PropertyChanged += ServiceOnPropertyChanged;

            // Create a shell frame shadow for mobile and desktop
            if (DeviceHelper.IsDesktop || DeviceHelper.IsMobile)
                ShellFrame.CreateElementShadow(new Vector3(0, 0, 0), 20, new Color {A = 52, R = 0, G = 0, B = 0},
                    ShellFrameShadow);

            // Events for Xbox
            if (DeviceHelper.IsXbox)
            {
                // Pane is hidden by default
                MainSplitView.IsPaneOpen = false;
                MainSplitView.DisplayMode = SplitViewDisplayMode.CompactOverlay;
                MainSplitView.Margin = new Thickness();

                // Center all navigation icons
                NavbarScrollViewer.VerticalAlignment = VerticalAlignment.Center;

                // Show background blur image
                XboxOnlyGrid.Visibility = Visibility.Visible;
                ShellFrame.Background = new SolidColorBrush(Colors.Transparent);

                // Splitview pane gets background
                SplitViewPaneGrid.Background =
                    Application.Current.Resources["InAppBackgroundBrush"] as CustomAcrylicBrush;

                // Make xbox selection easy to see
                Application.Current.Resources["CircleButtonStyle"] =
                    Application.Current.Resources["XboxCircleButtonStyle"];
            }

            // Events for Mobile
            if (DeviceHelper.IsMobile)
            {
                // Amoled Magic
                Application.Current.Resources["ShellBackground"] =
                    new SolidColorBrush(Application.Current.RequestedTheme == ApplicationTheme.Dark
                        ? Colors.Black
                        : Colors.White);

                // Splitview pane gets background
                SplitViewPaneGrid.Background =
                    Application.Current.Resources["InAppBackgroundBrush"] as CustomAcrylicBrush;

                MainSplitView.IsPaneOpen = false;
                MainSplitView.DisplayMode = SplitViewDisplayMode.Overlay;

                MobileMenu.Visibility = Visibility.Visible;
                HamburgerButton.Visibility = Visibility.Collapsed;
            }

            // Focus on the root frame
            RootFrame.Focus(FocusState.Programmatic);
        }

        private void ServiceOnPropertyChanged(object o, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (propertyChangedEventArgs.PropertyName != "CurrentTrack")
                return;

            if (Service.CurrentTrack == null || !DeviceHelper.IsDesktop ||
                RootFrame.CurrentSourcePageType == typeof(NowPlayingView))
                HideNowPlayingBar();
            else
                ShowNowPlayingBar();
        }

        /// <summary>
        ///     Used to access the playback service from the UI
        /// </summary>
        public PlaybackService Service => PlaybackService.Current;

        public void Dispose()
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested;
            Service.PropertyChanged -= ServiceOnPropertyChanged;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            if (App.OverrideBackEvent)
            {
                e.Handled = true;
            }
            else
            {
                if (RootFrame.SourcePageType == typeof(BlankPage))
                {
                    RootFrame.BackStack.Clear();
                    RootFrame.Navigate(typeof(HomeView));
                    e.Handled = true;
                }
                else
                {
                    if (RootFrame.CanGoBack)
                    {
                        RootFrame.GoBack();
                        e.Handled = true;
                    }
                    else
                    {
                        RootFrame.Navigate(typeof(HomeView));
                        e.Handled = true;
                    }
                }
            }
        }

        private async Task PerformAsyncWork(string path)
        {
            App.IsLoading = true;

            // Set the app language
            ApplicationLanguages.PrimaryLanguageOverride =
                string.IsNullOrEmpty(SettingsService.Current.CurrentAppLanguage)
                    ? ApplicationLanguages.Languages[0]
                    : SettingsService.Current.CurrentAppLanguage;

            // Set the on back requested event
            SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;

            // Navigate to the first page
            await HandleProtocolAsync(path);

            // Test Version and tell user app upgraded
            HandleNewAppVersion();

            // Clear the unread badge
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();

            // The methods below are sorted into try catch groups. many of them can fail, but they are not important
            try
            {
                // Get the store and check for app updates
                var updates = await StoreContext.GetDefault().GetAppAndOptionalStorePackageUpdatesAsync();

                // If we have updates navigate to the update page where we
                // ask the user if they would like to update or not (depending
                // if the update is mandatory or not).
                if (updates.Count > 0)
                    await new PendingUpdateDialog().ShowAsync();
            }
            catch (Exception e)
            {
                TelemetryService.Current.TrackException(e);
            }

            try
            {
                var engagementManager = StoreServicesEngagementManager.GetDefault();
                await engagementManager.RegisterNotificationChannelAsync();
            }
            catch (Exception e)
            {
                TelemetryService.Current.TrackException(e);
            }

            try
            {
                // Install Cortana Voice Commands
                var vcdStorageFile = await Package.Current.InstalledLocation.GetFileAsync(@"SoundByteCommands.xml");
                await VoiceCommandDefinitionManager.InstallCommandDefinitionsFromStorageFileAsync(vcdStorageFile);
            }
            catch (Exception e)
            {
                TelemetryService.Current.TrackException(e);
            }

            try
            {
                // Register the background task
                if (!BackgroundTaskHelper.IsBackgroundTaskRegistered("NotificationTask"))
                    BackgroundTaskHelper.Register("NotificationTask", "SoundByte.Notifications.NotificationTask",
                        new TimeTrigger(15, false));
            }
            catch (Exception e)
            {
                TelemetryService.Current.TrackException(e);
            }
        }

        private void HandleNewAppVersion()
        {
            var currentAppVersionString = Package.Current.Id.Version.Major + "." + Package.Current.Id.Version.Minor +
                                          "." + Package.Current.Id.Version.Build;

            // Get stored app version (this will stay the same when app is updated)
            var storedAppVersionString = SettingsService.Current.AppStoredVersion;

            // Save the new app version
            SettingsService.Current.AppStoredVersion = currentAppVersionString;

            // If the stored version is null, set the temp to 0, and the version to the actual version
            if (!string.IsNullOrEmpty(storedAppVersionString))
            {
                // Convert the current app version
                var currentAppVersion = new Version(currentAppVersionString);
                // Convert the stored app version
                var storedAppVersion = new Version(storedAppVersionString);

                if (currentAppVersion <= storedAppVersion)
                    return;
            }

            // Generate a notification
            var toastContent = new ToastContent
            {
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric
                    {
                        Children =
                        {
                            new AdaptiveText
                            {
                                Text = "SoundByte"
                            },

                            new AdaptiveText
                            {
                                Text = string.IsNullOrEmpty(storedAppVersionString)
                                    ? "Thank you for downloading SoundByte!"
                                    : $"SoundByte was updated to version {currentAppVersionString}."
                            }
                        }
                    }
                }
            };

            // Show the notification
            var toast = new ToastNotification(toastContent.GetXml()) {ExpirationTime = DateTime.Now.AddMinutes(30)};
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        #region Protocol

        public async Task HandleProtocolAsync(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    if (path == "playUserLikes" || path == "shufflePlayUserLikes")
                    {
                        // Get and load the user liked items
                        var userLikes = new LikeModel(SoundByteService.Current.SoundCloudUser);

                        while (userLikes.HasMoreItems)
                            await userLikes.LoadMoreItemsAsync(500);

                        // Play the list of items
                        await PlaybackService.Current.StartMediaPlayback(userLikes.ToList(), path,
                            path == "shufflePlayUserLikes");

                        // Navigate to the now playing screen
                        RootFrame.Navigate(typeof(NowPlayingView));

                        return;
                    }

                    var parser = DeepLinkParser.Create(path);

                    var section = parser.Root.Split('/')[0].ToLower();
                    var page = parser.Root.Split('/')[1].ToLower();

                    App.IsLoading = true;
                    if (section == "core")
                        switch (page)
                        {
                            case "track":
                                var track = await SoundByteService.Current.GetAsync<Track>($"/tracks/{parser["id"]}");

                                var startPlayback =
                                    await PlaybackService.Current.StartMediaPlayback(new List<Track> {track},
                                        $"Protocol-{track.Id}");

                                if (!startPlayback.success)
                                    await new MessageDialog(startPlayback.message, "Error playing track.").ShowAsync();
                                break;
                            case "playlist":
                                var playlist =
                                    await SoundByteService.Current.GetAsync<Playlist>($"/playlists/{parser["id"]}");
                                App.NavigateTo(typeof(Views.Playlist), playlist);
                                return;
                            case "user":
                                var user = await SoundByteService.Current.GetAsync<User>($"/users/{parser["id"]}");
                                App.NavigateTo(typeof(UserView), user);
                                return;
                        }
                }
                catch (Exception)
                {
                    await new MessageDialog("The specified protocol is not correct. App will now launch as normal.")
                        .ShowAsync();
                }
                App.IsLoading = false;
            }

            RootFrame.Navigate(typeof(HomeView));
        }

        #endregion

        private void NavigateHome(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(HomeView));
        }

        private void NavigateDonate(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(DonateView));
        }

        private void NavigateSettings(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(SettingsView));
        }

        private void NavigateNotifications(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(NotificationsView));
        }

        private void NavigateHistory(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(HistoryView));
        }

        private void NavigateLikes(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(LikesView));
        }

        private void NavigateAccounts(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(AccountView));
        }

        private void NavigateSets(object sender, RoutedEventArgs e)
        {
            if (BlockNavigation) return;

            RootFrame.Navigate(typeof(PlaylistsView));
        }

        private void ShellFrame_Navigated(object sender, NavigationEventArgs e)
        {
            BlockNavigation = true;

            // Update the side bar
            switch (((Frame) sender).SourcePageType.Name)
            {
                case "HomeView":
                    HomeTab.IsChecked = true;
                    break;
                case "DonateView":
                    DonateTab.IsChecked = true;
                    break;
                case "LikesView":
                    LikesTab.IsChecked = true;
                    break;
                case "PlaylistsView":
                    SetsTab.IsChecked = true;
                    break;
                case "NotificationsView":
                    NotificationsTab.IsChecked = true;
                    break;
                case "HistoryView":
                    HistoryTab.IsChecked = true;
                    break;
                case "AccountView":
                    AccountTab.IsChecked = true;
                    break;
                case "SettingsView":
                    SettingsTab.IsChecked = true;
                    break;
                case "AboutView":
                    SettingsTab.IsChecked = true;
                    break;
                default:
                    UnknownTab.IsChecked = true;
                    break;
            }

            RootFrame.Focus(FocusState.Keyboard);

            if (((Frame) sender).SourcePageType == typeof(HomeView) ||
                ((Frame) sender).SourcePageType == typeof(MainShell))
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Collapsed;
            else
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                    AppViewBackButtonVisibility.Visible;

            // Update the UI depending if we are logged in or not
            if (SoundByteService.Current.IsSoundCloudAccountConnected ||
                SoundByteService.Current.IsFanBurstAccountConnected)
                ShowLoginContent();
            else
                ShowLogoutContent();

            if (DeviceHelper.IsDesktop)
                if (((Frame) sender).SourcePageType == typeof(NowPlayingView))
                {
                    MainSplitView.IsPaneOpen = false;
                    MainSplitView.CompactPaneLength = 0;

                    HideNowPlayingBar();

                    MainSplitView.Margin = new Thickness {Bottom = 0, Top = 0};
                }
                else
                {
                    MainSplitView.CompactPaneLength = 84;

                    if (Service.CurrentTrack == null)
                        HideNowPlayingBar();
                    else
                        ShowNowPlayingBar();
                }

            if (DeviceHelper.IsXbox)
                if (((Frame) sender).SourcePageType == typeof(NowPlayingView))
                {
                    MainSplitView.IsPaneOpen = false;
                    MainSplitView.CompactPaneLength = 0;
                }
                else
                {
                    MainSplitView.IsPaneOpen = false;
                    MainSplitView.CompactPaneLength = 84;
                }

            BlockNavigation = false;
        }

        private void HideNowPlayingBar()
        {
            UnknownTab.IsChecked = true;
            NowPlaying.Visibility = Visibility.Collapsed;

            MainSplitView.Margin = DeviceHelper.IsXbox
                ? new Thickness {Bottom = 0, Top = 0}
                : new Thickness {Bottom = 0, Top = 32};
        }

        private void ShowNowPlayingBar()
        {
            NowPlaying.Visibility = Visibility.Visible;
            MainSplitView.Margin = new Thickness {Bottom = 64, Top = 32};
        }

        private void SearchBox_SearchSubmitted(object sender, RoutedEventArgs e)
        {
            App.NavigateTo(typeof(Search), (e as SearchBox.SearchEventArgs)?.Keyword);
        }

        // Login and Logout events. This is used to display what pages
        // are visiable to the user.
        public void ShowLoginContent()
        {
            LikesTab.Visibility = Visibility.Visible;
            SetsTab.Visibility = Visibility.Visible;
            NotificationsTab.Visibility = Visibility.Visible;
            HistoryTab.Visibility = Visibility.Visible;
            AccountTab.Content = "Connected Accounts";
        }

        public void ShowLogoutContent()
        {
            LikesTab.Visibility = Visibility.Collapsed;
            SetsTab.Visibility = Visibility.Collapsed;
            NotificationsTab.Visibility = Visibility.Collapsed;
            HistoryTab.Visibility = Visibility.Collapsed;
            AccountTab.Content = "Login";
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
        }

        #region Getters and Setters

        /// <summary>
        ///     Used to block navigation from happening when
        ///     updating the UI for sidebar
        /// </summary>
        private bool BlockNavigation { get; set; }

        /// <summary>
        ///     Get the root frame, if no root frame exists,
        ///     we wait 150ms and call the getter again.
        /// </summary>
        public Frame RootFrame
        {
            get
            {
                if (ShellFrame != null) return ShellFrame;

                Task.Delay(TimeSpan.FromMilliseconds(150));

                return RootFrame;
            }
        }

        #endregion
    }
}