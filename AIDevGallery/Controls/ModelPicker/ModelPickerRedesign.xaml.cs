// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace AIDevGallery.Controls;

// ponytail: sandbox canvas for the picker redesign. Mock-only, decoupled from the live
// ModelOrApiPicker and the model-loading pipeline. Port into the live picker once the design is approved.
internal sealed partial class ModelPickerRedesign : UserControl
{
    public ModelPickerRedesign()
    {
        this.InitializeComponent();
    }

    public void Show()
    {
        this.Visibility = Visibility.Visible;
        CloseButton.Focus(FocusState.Programmatic);
    }

    public void Hide()
    {
        this.Visibility = Visibility.Collapsed;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void SmokeGrid_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) => Hide();

    // ponytail: mock doc links. Real picker would source URLs from ModelDetails, not hardcoded Tags.
    private async void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string url
            && System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri))
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    // ponytail: mock download-confirm flow to communicate the interaction to the team.
    // Real picker would kick off the actual model download on Primary and reflect progress.
    private async void Phi35Radio_Checked(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Download required",
            Content = "Phi 3.5 Vision CPU (3.0 GB) needs to be downloaded before you can use it. Download now?",
            PrimaryButtonText = "Download",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            ApiRadio.IsChecked = true; // revert selection on cancel
        }
    }
}
