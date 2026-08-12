// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AIDevGallery.Controls;

internal sealed partial class GettingStartedSection : UserControl
{
    public GettingStartedSection()
    {
        this.InitializeComponent();
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } || string.IsNullOrEmpty(id))
        {
            return;
        }

        if (App.FindScenarioById(id) is { } scenario)
        {
            App.MainWindow.NavigateToPage(scenario);
        }
        else
        {
            App.MainWindow.Navigate(id);
        }
    }
}