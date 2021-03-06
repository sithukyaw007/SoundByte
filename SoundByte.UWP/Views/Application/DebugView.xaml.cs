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

using Windows.UI.Xaml;
using SoundByte.UWP.Services;

namespace SoundByte.UWP.Views.Application
{
    /// <summary>
    /// Blank page used for debugging.
    /// </summary>
    public sealed partial class DebugView
    {
        public DebugView()
        {
            InitializeComponent();
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            await NavigationService.Current.CallDialogAsync(Command.Text);
        }
    }
}
