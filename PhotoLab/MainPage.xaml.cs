

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System.Profile;
using Windows.UI.Xaml;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Diagnostics;

namespace PhotoLab
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        public static MainPage Current;
        private ImageFileInfo persistedItem;

        public ObservableCollection<ImageFileInfo> Images { get; } = new ObservableCollection<ImageFileInfo>();
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            Current = this;
        }
        public void UpdatePersistedItem(ImageFileInfo item)
        {
            persistedItem = item;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                AppViewBackButtonVisibility.Collapsed;

            if (Images.Count == 0)
            {
                await GetItemsAsync();
            }

            base.OnNavigatedTo(e);
        }

        // Called by the Loaded event of the ImageGridView.
        private async void StartConnectedAnimationForBackNavigation()
        {
            // Run the connected animation for navigation back to the main page from the detail page.
            if (persistedItem != null)
            {
                ImageGridView.ScrollIntoView(persistedItem);
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("backAnimation");
                if (animation != null)
                {
                    await ImageGridView.TryStartConnectedAnimationAsync(animation, persistedItem, "ItemImage");
                }
            }
        }
        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a FileOpenPicker to let the user select a file to upload
            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            // Filter the file types to restrict the selection to specific file types
            filePicker.FileTypeFilter.Add(".jpg");
            filePicker.FileTypeFilter.Add(".png");
            filePicker.FileTypeFilter.Add(".gif");

            // Show the file picker dialog and await the user's file selection
            StorageFile file = await filePicker.PickSingleFileAsync();

            if (file != null)
            {
                try
                {
                    // Get the folder where you want to save the file (Assets\Samples)
                    StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
                    StorageFolder samplesFolder = await appInstalledFolder.GetFolderAsync("Assets\\Samples");

                    // Create a copy of the selected file in the Samples folder
                    await file.CopyAsync(samplesFolder, file.Name, NameCollisionOption.GenerateUniqueName);

                    // Optionally, you can add the newly uploaded file to the Images collection
                    // assuming that Images is an ObservableCollection<ImageFileInfo>
                    Images.Add(await LoadImageInfo(file));

                    // Optionally, perform any additional actions, such as updating the UI
                    // to reflect the uploaded file.
                }
                catch (Exception ex)
                {
                    // Handle any exceptions that might occur during the upload process
                    // (e.g., insufficient permissions, file already exists, etc.)
                    // You can display an error message or log the exception for debugging.
                    Debug.WriteLine($"Upload failed: {ex.Message}");
                }
            }
        }
        private void ImageGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Prepare the connected animation for navigation to the detail page.
            persistedItem = e.ClickedItem as ImageFileInfo;
            ImageGridView.PrepareConnectedAnimation("itemAnimation", e.ClickedItem, "ItemImage");

            this.Frame.Navigate(typeof(DetailPage), e.ClickedItem);
        }

        private async Task GetItemsAsync()
        {
            QueryOptions options = new QueryOptions();
            options.FolderDepth = FolderDepth.Deep;
            options.FileTypeFilter.Add(".jpg");
            options.FileTypeFilter.Add(".png");
            options.FileTypeFilter.Add(".gif");

            StorageFolder appInstalledFolder = Package.Current.InstalledLocation;
            StorageFolder picturesFolder = await appInstalledFolder.GetFolderAsync("Assets\\Samples");

            var result = picturesFolder.CreateFileQueryWithOptions(options);

            IReadOnlyList<StorageFile> imageFiles = await result.GetFilesAsync();
            bool unsupportedFilesFound = false;
            foreach (StorageFile file in imageFiles)
            {
                // Only files on the local computer are supported. 
                // Files on OneDrive or a network location are excluded.
                if (file.Provider.Id == "computer")
                {
                    Images.Add(await LoadImageInfo(file));
                }
                else
                {
                    unsupportedFilesFound = true;
                }
            }

            if (unsupportedFilesFound == true)
            {
                ContentDialog unsupportedFilesDialog = new ContentDialog
                {
                    Title = "Unsupported images found",
                    Content = "This sample app only supports images stored locally on the computer. We found files in your library that are stored in OneDrive or another network location. We didn't load those images.",
                    CloseButtonText = "Ok"
                };

                ContentDialogResult resultNotUsed = await unsupportedFilesDialog.ShowAsync();
            }
        }

        public async static Task<ImageFileInfo> LoadImageInfo(StorageFile file)
        {
            var properties = await file.Properties.GetImagePropertiesAsync();
            ImageFileInfo info = new ImageFileInfo(
                properties, file,
                file.DisplayName, file.DisplayType);

            return info;
        }

        public double ItemSize
        {
            get => _itemSize;
            set
            {
                if (_itemSize != value)
                {
                    _itemSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemSize)));
                }
            }
        }
        private double _itemSize;

        private void DetermineItemSize()
        {
            if (FitScreenToggle != null
                && FitScreenToggle.IsOn == true
                && ImageGridView != null
                && ZoomSlider != null)
            {
                // The 'margins' value represents the total of the margins around the
                // image in the grid item. 8 from the ItemTemplate root grid + 8 from
                // the ItemContainerStyle * (Right + Left). If those values change,
                // this value needs to be updated to match.
                int margins = (int)this.Resources["LargeItemMarginValue"] * 4;
                double gridWidth = ImageGridView.ActualWidth - (int)this.Resources["DefaultWindowSidePaddingValue"];
                double ItemWidth = ZoomSlider.Value + margins;
                // We need at least 1 column.
                int columns = (int)Math.Max(gridWidth / ItemWidth, 1);

                // Adjust the available grid width to account for margins around each item.
                double adjustedGridWidth = gridWidth - (columns * margins);

                ItemSize = (adjustedGridWidth / columns);
            }
            else
            {
                ItemSize = ZoomSlider.Value;
            }
        }

        private void ImageGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                var templateRoot = args.ItemContainer.ContentTemplateRoot as Grid;
                var image = (Image)templateRoot.FindName("ItemImage");

                image.Source = null;
            }

            if (args.Phase == 0)
            {
                args.RegisterUpdateCallback(ShowImage);
                args.Handled = true;
            }
        }

        private async void ShowImage(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Phase == 1)
            {
                // It's phase 1, so show this item's image.
                var templateRoot = args.ItemContainer.ContentTemplateRoot as Grid;
                var image = (Image)templateRoot.FindName("ItemImage");
                image.Opacity = 100;

                var item = args.Item as ImageFileInfo;

                try
                {
                    image.Source = await item.GetImageThumbnailAsync();
                }
                catch (Exception)
                {
                    // File could be corrupt, or it might have an image file
                    // extension, but not really be an image file.
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.UriSource = new Uri(image.BaseUri, "Assets/StoreLogo.png");
                    image.Source = bitmapImage;
                }
            }
        }
    }
}
