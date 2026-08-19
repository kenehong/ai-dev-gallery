// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Helpers;
using AIDevGallery.Models;
using AIDevGallery.Telemetry.Events;
using AIDevGallery.Utils;
using AIDevGallery.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace AIDevGallery.Controls.ModelPickerViews;

internal sealed partial class OnnxPickerView : BaseModelPickerView
{
    private List<ModelDetails> models = [];
    private List<ModelType>? modelTypes;
    private string? selectedModelId;
    private ObservableCollection<OnnxModelPickerRow> ModelRows { get; } = [];

    public OnnxPickerView()
    {
        this.InitializeComponent();

        App.ModelCache.CacheStore.ModelsChanged += CacheStore_ModelsChanged;
    }

    public override Task Load(List<ModelType> types)
    {
        VisualStateManager.GoToState(this, "SideCustomModelInfoCollapsed", true);
        modelTypes = types;

        ResetAndLoadModelList();

        var isAddModelButtonsVisible = false;

        if (types.Contains(ModelType.LanguageModels))
        {
            VisualStateManager.GoToState(this, "SideCustomModelInfoVisible", true);
            AddHFModelButton.Visibility = Visibility.Visible;
            isAddModelButtonsVisible = true;
        }

        // local models supported for types
        if (types.Contains(ModelType.LanguageModels) || models.IsModelsDetailsListUploadCompatible())
        {
            AddLocalModelButton.Visibility = Visibility.Visible;
            isAddModelButtonsVisible = true;
        }

        if (isAddModelButtonsVisible)
        {
            AddModelButtons.Visibility = Visibility.Visible;
        }

        return Task.CompletedTask;
    }

    private void ResetAndLoadModelList()
    {
        models.Clear();
        ModelRows.Clear();

        if (modelTypes == null || modelTypes.Count == 0)
        {
            return;
        }

        foreach (ModelType type in modelTypes)
        {
            models.AddRange(ModelDetailsHelper.GetModelDetailsForModelType(type));
        }

        if (models == null || models.Count == 0)
        {
            return;
        }

        HashSet<string> modelUrls = [];
        foreach (var model in models)
        {
            if (!model.IsOnnxModel() || !modelUrls.Add(model.Url))
            {
                continue;
            }

            if (model.Compatibility.CompatibilityState == ModelCompatibilityState.NotCompatible)
            {
                continue;
            }

            var row = new OnnxModelPickerRow(model, App.ModelCache.IsModelCached(model.Url))
            {
                IsSelected = model.Id == selectedModelId && App.ModelCache.IsModelCached(model.Url)
            };
            ModelRows.Add(row);
        }
    }

    private void CacheStore_ModelsChanged(ModelCacheStore sender)
    {
        DispatcherQueue.TryEnqueue(ResetAndLoadModelList);
    }

    public override void SelectModel(ModelDetails? modelDetails)
    {
        selectedModelId = modelDetails?.Id;
        foreach (var row in ModelRows)
        {
            row.IsSelected = row.IsInstalled && row.ModelDetails.Id == selectedModelId;
        }
    }

    private void SelectModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: OnnxModelPickerRow row } || !row.IsInstalled)
        {
            return;
        }

        SelectModel(row.ModelDetails);
        OnSelectedModelChanged(this, row.ModelDetails);
    }

    private void OpenModelFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var cachedModel = App.ModelCache.GetCachedModel(details.Url);
            if (cachedModel != null)
            {
                var path = cachedModel.Path;
                if (path != null)
                {
                    if (cachedModel.IsFile)
                    {
                        path = Path.GetDirectoryName(path);
                    }

                    OpenModelFolderEvent.Log(cachedModel.Url);

                    Process.Start("explorer.exe", path!);
                }
            }
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            ContentDialog deleteDialog = new()
            {
                Title = "Delete model",
                Content = "Are you sure you want to delete this model? You can download it again from this page.",
                PrimaryButtonText = "Yes",
                XamlRoot = this.Content.XamlRoot,
                PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
                CloseButtonText = "No"
            };

            var result = await deleteDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await App.ModelCache.DeleteModelFromCache(details.Url);
                ResetAndLoadModelList();
            }
        }
    }

    private void ModelCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            App.MainWindow.Navigate("Models", details.Id);
        }
    }

    private void ApiDocumentation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            App.MainWindow.Navigate("apis", details);
        }
    }

    private void CopyModelPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var dataPackage = new DataPackage();
            var modelCache = App.ModelCache.Models.FirstOrDefault(m => m.Details.Id == details.Id);
            if (modelCache != null)
            {
                dataPackage.SetText(modelCache.Path);
                Clipboard.SetContentWithOptions(dataPackage, null);
            }
        }
    }

    private void ViewLicense_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem btn && btn.Tag is ModelDetails details)
        {
            var license = LicenseInfo.GetLicenseInfo(details.License);
            string licenseUrl;
            if (license.LicenseUrl != null)
            {
                licenseUrl = license.LicenseUrl;
            }
            else
            {
                licenseUrl = details.Url;
            }

            Process.Start(new ProcessStartInfo()
            {
                FileName = licenseUrl,
                UseShellExecute = true
            });
        }
    }

    private async void DownloadModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OnnxModelPickerRow row })
        {
            var downloadSource = row.ModelDetails.Url.StartsWith("https://github.com", StringComparison.InvariantCultureIgnoreCase) ? "GitHub" : "Hugging Face";
            var license = LicenseInfo.GetLicenseInfo(row.ModelDetails.License);

            ModelNameTxt.Text = row.ModelDetails.Name;
            ModelSourceTxt.Text = downloadSource;
            ModelLicenseLink.NavigateUri = new Uri(license.LicenseUrl ?? row.ModelDetails.Url);
            ModelLicenseLabel.Text = license.Name;

            WarningInfoBar.IsOpen = false;
            if (row.ModelDetails.Compatibility.CompatibilityState != ModelCompatibilityState.Compatible)
            {
                WarningInfoBar.Message = row.ModelDetails.Compatibility.CompatibilityIssueDescription;
                WarningInfoBar.IsOpen = true;
            }

            AgreeCheckBox.IsChecked = false;

            var output = await DownloadDialog.ShowAsync();

            if (output == ContentDialogResult.Primary)
            {
                row.StartDownload();
            }
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OnnxModelPickerRow row })
        {
            row.CancelDownload();
        }
    }

    private void RetryDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OnnxModelPickerRow row })
        {
            row.StartDownload();
        }
    }

    private void AddHFModelButton_Click(object sender, RoutedEventArgs e)
    {
        AddHFModelView.Visibility = Visibility.Visible;
        ModelView.Visibility = Visibility.Collapsed;
    }

    private void AddHFModelView_CloseRequested(object sender)
    {
        AddHFModelView.Visibility = Visibility.Collapsed;
        ModelView.Visibility = Visibility.Visible;
    }

    private async void AddLocalModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (modelTypes == null)
        {
            return;
        }

        try
        {
            if (modelTypes.Contains(ModelType.LanguageModels))
            {
                await UserAddedModelUtil.OpenAddLanguageModelFlow(Content.XamlRoot);
            }
            else
            {
                await UserAddedModelUtil.OpenAddModelFlow(Content.XamlRoot, modelTypes);
            }

            ResetAndLoadModelList();
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private void OpenAIToolkitButton_Click(object sender, RoutedEventArgs e)
    {
        string toolkitDeeplink = AIToolkitHelper.CreateAiToolkitDeeplink(AIToolkitAction.Conversion);
        bool wasDeeplinkSuccessful = true;
        try
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = toolkitDeeplink,
                UseShellExecute = true
            });
        }
        catch
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = "https://learn.microsoft.com/en-us/windows/ai/toolkit/",
                UseShellExecute = true
            });
            wasDeeplinkSuccessful = false;
        }
        finally
        {
            AIToolkitActionClickedEvent.Log(AIToolkitHelper.AIToolkitActionInfos[AIToolkitAction.Conversion].QueryName, "null", wasDeeplinkSuccessful);
        }
    }

    private void ViewDocumentationButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = "https://aka.ms/winml-gallery-tutorial",
            UseShellExecute = true
        });
    }

    private async void ShowException(Exception? ex, string? optionalMessage = null)
    {
        var msg = $"Error:\n{ex?.Message}{(optionalMessage != null ? "\n" + optionalMessage : string.Empty)}";

        var errorText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = msg,
            IsTextSelectionEnabled = true,
        };

        ContentDialog exceptionDialog = new()
        {
            Title = "Something went wrong",
            Content = errorText,
            PrimaryButtonText = "Copy error details",
            XamlRoot = App.MainWindow.Content.XamlRoot,
            CloseButtonText = "Close",
            PrimaryButtonStyle = (Style)App.Current.Resources["AccentButtonStyle"],
        };

        var result = await exceptionDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string exceptionDetails = string.IsNullOrWhiteSpace(optionalMessage) ? string.Empty : optionalMessage + "\n";

            if (ex != null)
            {
                exceptionDetails += GetExceptionDetails(ex);
            }

            DataPackage dataPackage = new DataPackage();
            dataPackage.SetText(exceptionDetails);
            Clipboard.SetContent(dataPackage);
        }
    }

    private string GetExceptionDetails(Exception ex)
    {
        var innerExceptionData = ex.InnerException == null ? string.Empty :
            $"Inner Exception:\n{GetExceptionDetails(ex.InnerException)}";
        string details = $@"Type: {ex.GetType().Name}
Message: {ex.Message}
StackTrace: {ex.StackTrace}
{innerExceptionData}";
        return details;
    }
}