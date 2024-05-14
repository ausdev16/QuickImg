using System;
using System.Diagnostics;
using System.Collections.Generic;

using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.AccessCache;

using Windows.UI.Core;
using Windows.UI.ViewManagement;

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;
using System.Collections.ObjectModel;
using Windows.UI;
using Windows.Graphics.Display;
using Windows.UI.Popups;
using Windows.Storage.Provider;
using System.Linq;
using System.IO;


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace QuickImg
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    public sealed partial class MainPage : Page
    {
        private string lastErrorMessage;        

        private StorageFile file;
        private StorageFile nextFile;
        private ImageDetails details;

        private List<StorageFile> imageFiles;
        
        //bool isFullScreen;

        private ViewMode viewMode;

        ApplicationDataContainer localSettings;
        bool disableAnimation;

        bool navigatedTo;
        bool justLoaded;
        bool zoomSliderChanged;
        double newAngle; // Required to see when rotation animation is completed, in order to re-enable rotation buttons.
        bool firstImage; // This is hack so that the zoom level is set correctly on the first image if DPI override is selected
        bool doNotConfirmDelete;
        
        public double DPIOverrideFraction;

        public ObservableCollection<FolderWToken> folders;

        // CONSTRUCTOR
        public MainPage()
        {
            this.InitializeComponent();

            navigatedTo = false;

            lastErrorMessage = "";

            details = new ImageDetails();            
            
            //isFullScreen = false;
            //viewMode = ViewMode.Fit;
                                              
            justLoaded = false;
            zoomSliderChanged = false;
            
            firstImage = true;

            doNotConfirmDelete = false;

            // Annoying hack to add padding to the right button if the scrollviewer is visible.
            scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ScrollableHeightProperty, scrollViewer_ScrollableHeightChanged);

            Window.Current.Activated += Current_Activated;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            localSettings = ApplicationData.Current.LocalSettings;

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            ThemeHelper.SetTheme(titleBar, this, SettingsHelper.GetTheme(localSettings));

            DPIOverrideFraction = SettingsHelper.GetDPIOverrideFraction(localSettings);

            InitialViewMode initialViewMode = SettingsHelper.GetInitialViewMode(localSettings);
            if (initialViewMode == InitialViewMode.ActualSize)
            {
                viewMode = ViewMode.ActualPixels;
            }
            else if (initialViewMode == InitialViewMode.Fit)
            {
                viewMode = ViewMode.Fit;
            }
            else // View Mode Selection is Last Used
            {
                viewMode = SettingsHelper.GetSavedViewMode(localSettings);
            }

            disableAnimation = SettingsHelper.GetDisableAnimation(localSettings);
            statusGrid.Visibility = SettingsHelper.GetStatusBarVisibility(localSettings);
                       

            if (Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.IsSupported())
            {
                this.feedbackButton.Visibility = Visibility.Visible;
            }

            navigatedTo = true;
        }

        private async Task AddAllowedFoldersAsync()
        {
            folders = new ObservableCollection<FolderWToken>();

            var entries = StorageApplicationPermissions.FutureAccessList.Entries;
            foreach (var entry in entries)
            {
                var folder = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(entry.Token);
                FolderWToken folderWToken = new FolderWToken(folder, entry.Token);                
                folders.Add(folderWToken);
            }

            folderListBox.ItemsSource = folders;
        }

        public async Task HandleFileActivatedAsync(IStorageItem item)
        {
            if (!item.IsOfType(StorageItemTypes.File))
            {
                statusTextBlock.Text = "The activated item is not a file.";
                return;
            }

            try
            {
                file = await StorageFile.GetFileFromPathAsync(item.Path);
            }
            catch (Exception e)
            {
                if (e.HResult == -2147024891)
                {
                    // Ask user if they would like to add it to the allowed locations
                    askToAddNewLocatoin(item);
                }
                
                statusTextBlock.Text = "ERROR: " + e.Message;
                return;
            }

            await openWrapperAsync();
        }

        // OPEN BUTTON
        private async void openAppBarButton_Click(object sender, RoutedEventArgs e)
        {            
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            foreach (string fileType in Constants.FILE_TYPES)
            {
                picker.FileTypeFilter.Add(fileType);
            }     

            file = await picker.PickSingleFileAsync();
            
            if (file != null)
            {
                await openWrapperAsync();
            }
            else
            {
                statusTextBlock.Text = "Operation cancelled.";
            }
        }

        // TO DO: Merge into calling / internal methods.
        private async Task openWrapperAsync()
        {
            setBusy(true);

            image.Source = null;

            resetRotation();
            
            details.SetImageDetails(file);
            ImageType type = FileHelper.GetImageType(details.Type);

            bool success = await handleOpenAsync(type);
            if (success)
            {
                // TO DO: This is a bit hacky
                // Set inital zoom level if DPI override is on
                if (viewMode == ViewMode.ActualPixels && DPIOverrideFraction != 1 && firstImage)
                {
                    bool animate = disableAnimation;
                    disableAnimation = true; // We do not want to "animate" the empty canvas on original load
                    handleZoomToActualSize(); // This set's the status Text
                    disableAnimation = animate;
                    firstImage = false;

                    setStatusText(details.GetDimensions());
                }
                else
                {
                    setStatusText();
                }
            }
            else
            {
                statusTextBlock.Text = "ERROR: " + lastErrorMessage;
            }

            enableImageManipulationButtons();

            setBusy(false);
        }

        /// <summary>
        /// Sets rotation back to 0 degrees when opening a new image, using the file picker or the back/next buttons.
        /// </summary>
        private void resetRotation()
        {
            

            if (rotateTransform.Angle != 0)
            {
                rotateTransform.Angle = 0;
                rotationTextBlock.Text = "0°";
            }
        }

        /// <summary>
        /// Enables Image Manipulation Buttons.
        /// That is, buttons which require a fully loaded image to work...
        /// This is used after an image has successfully loaded.
        /// </summary>
        private void enableImageManipulationButtons()
        {
            previousButton.IsEnabled = true;
            previousButton.Visibility = Visibility.Visible;
            nextButton.IsEnabled = true;
            nextButton.Visibility = Visibility.Visible;

            deleteAppBarButton.IsEnabled = true;

            rotateLeftAppBarButton.IsEnabled = true;
            rotateRightAppBarButton.IsEnabled = true;
            zoomToActualSizeAppBarButton.IsEnabled = true;
            fitToWindowAppBarButton.IsEnabled = true;
            zoomAppBarButton.IsEnabled = true;

            fullScreenAppBarButton.IsEnabled = true;
        }

        /// <summary>
        /// Disables Image Manipulation Buttons.
        /// That is, buttons which require a fully loaded image to work...
        /// This is used when a new image is being loaded.
        /// </summary>
        private void disableImageManipulationButtons()
        {
            previousButton.IsEnabled = false;
            nextButton.IsEnabled = false;

            deleteAppBarButton.IsEnabled = false;

            rotateLeftAppBarButton.IsEnabled = false;
            rotateRightAppBarButton.IsEnabled = false;
            zoomToActualSizeAppBarButton.IsEnabled = false;
            fitToWindowAppBarButton.IsEnabled = false;
            zoomAppBarButton.IsEnabled = false;

            fullScreenAppBarButton.IsEnabled = false;
        }

        /// <summary>
        /// Render's an image file after a file picker has succesfully opened the file.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private async Task<bool> handleOpenAsync(ImageType type)
        {
            bool success = false;

            try // TO DO: Split this into two methods?
            {
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    if (type == ImageType.Bitmap)
                    {
                        // Set the image source to the selected bitmap 
                        BitmapImage bitmapImage = new BitmapImage();

                        await bitmapImage.SetSourceAsync(fileStream);                        

                        details.Width = bitmapImage.PixelWidth;
                        details.Height = bitmapImage.PixelHeight;

                        image.Source = bitmapImage;

                        success = true;
                    }
                    else //if (type == ImageType.SVG)
                    {
                        SvgImageSource svgImage = new SvgImageSource();

                        details.Width = (int)scrollViewer.ViewportWidth;
                        details.Height = (int)scrollViewer.ViewportHeight;

                        svgImage.RasterizePixelWidth = details.Width;
                        svgImage.RasterizePixelHeight = details.Height;

                        await svgImage.SetSourceAsync(fileStream);

                        image.Source = svgImage;

                        success = true;
                    }
                }
            }
            catch (Exception e)
            {   
                lastErrorMessage = e.Message;

                return success;
            }

            Debug.WriteLine(DateTime.Now + " - Image source set");

            // Not setting these explicity, causes an odd bug with the scrollviewer when images are smaller than the viewport.
            // They are scalted to the full viewport size (or in some cases partially)
            // Even though the zoom factor is "1".
            // Setting these values seems to ensure the image size is correct for the scrollviewer in its caluclations.
            image.Width = details.Width;
            image.Height = details.Height;

            justLoaded = true;

            statusTextBlock.Text = ""; // Clear any previous error messages if present.
                        
            return success;
        }

        /// <summary>
        /// Sets the status text for a newly opened image file.
        /// TO DO: Use a bind instead?
        /// </summary>
        private void setStatusText(string displayedSize = "")
        {
            nameTextBlock.Text = details.DisplayName;
            typeTextBlock.Text = details.DisplayType;
            dimensionsTextBlock.Text = details.GetDimensions();

            // Another DPI override hack...
            if (displayedSize == "")
            {
                displayedSize = Math.Round(details.Width * scrollViewer.ZoomFactor / DPIOverrideFraction) + " x " + Math.Round(details.Height * scrollViewer.ZoomFactor / DPIOverrideFraction);
            }
            displayedSizeTextBlock.Text = displayedSize;
        }

        // DELETE BUTTON
        private void DeleteDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            // Ensure that the check box is unchecked each time the dialog opens.
            //ConfirmDeleteCheckBox.IsChecked = false;
        }
        private void ConfirmDeleteCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            doNotConfirmDelete = true;
        }

        private void ConfirmDeleteCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            doNotConfirmDelete = false;
        }
        private async void deleteAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            //ContentDialog deleteFileDialog = new ContentDialog
            //{
            //    Title = "Delete image permanently?",
            //    Content = "If you delete this image, you may not be able to recover it. Do you want to delete it?",
            //    PrimaryButtonText = "Delete",
            //    CloseButtonText = "Cancel"
            //};

            bool actionDelete;

            ContentDialogResult result = ContentDialogResult.None;
            if (!doNotConfirmDelete)
            {
                result = await DeleteDialog.ShowAsync();               
            }

            if (result == ContentDialogResult.Primary)
            {
                actionDelete = true;
            }
            else if (doNotConfirmDelete == true)
            {
                actionDelete = true;
            }
            else
            {
                actionDelete = false;
            }

            // Delete the file if the user clicked the primary button.
            /// Otherwise, do nothing.
            if (actionDelete)
            {
                setBusy(true);

                // Get the next file (before deleting, otherwise we won't have the current path).
                bool nextSuccess = await handleNextAsync();

                // Delete the file.
                bool success = await handleDeleteAsync();

                if (success)
                {
                    statusTextBlock.Text = "Image deleted successfully, attempting to open next image...";
                                        
                    if (nextSuccess)
                    {
                        file = nextFile;
                        nextFile = null;

                        details.SetImageDetails(file);
                        ImageType type = FileHelper.GetImageType(details.Type);

                        bool openSuccess = await handleOpenAsync(type);
                        if (openSuccess)
                        {
                            setStatusText();
                        }
                        else
                        {
                            statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                        }
                    }                    
                    else
                    {
                        file = null;

                        //statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                        statusTextBlock.Text = "No more images in folder.";

                        nameTextBlock.Text = "";
                        typeTextBlock.Text = "N/A";
                        dimensionsTextBlock.Text = "0 x 0";
                        rotationTextBlock.Text = "0°";
                        zoomTextBlock.Text = "100%";
                        displayedSizeTextBlock.Text = "0 x 0";

                        disableImageManipulationButtons();
                    }

                    resetRotation();
                }
                else
                {
                    statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                }

                setBusy(false);
            }
            else
            {
                // The user clicked the CLoseButton, pressed ESC, Gamepad B, or the system back button.
                // Do nothing.
            }
        }

        /// <summary>
        /// Delete's the currently opened image file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> handleDeleteAsync()
        {
            bool success = false;

            if (file == null)
            {
                lastErrorMessage = "No file reference!";
                image.Source = null;

                return success;
            }

            setBusy(true);

            try
            {
                // If there is a list of the images in the current directory, we need to remove so the Next/Prev buttons don't try to load it
                int index = imageFiles.FindIndex(fileToCompare => fileToCompare.Path == file.Path);
                imageFiles.RemoveAt(index);

                await file.DeleteAsync();

                file = null;
                image.Source = null;

                success = true;
            }
            catch (Exception e)
            {
                lastErrorMessage = e.Message;
            }

            setBusy(false);

            return success;
        }        

        /// <summary>
        /// Disables the command, previous/next buttons, and scroll viewer. Activates the progress ring.
        /// And vice versa.
        /// </summary>
        /// <param name="busy"></param>
        private void setBusy(bool busy)
        {
            if (busy)
            {
                commandBar.IsEnabled = false;
                previousButton.IsEnabled = false;
                previousButton.Visibility = Visibility.Collapsed;
                nextButton.IsEnabled = false;
                nextButton.Visibility = Visibility.Collapsed;
                scrollViewer.IsEnabled = false;
                progressRing.IsActive = true;
            }
            else
            {
                commandBar.IsEnabled = true;
                previousButton.IsEnabled = true;
                previousButton.Visibility = Visibility.Visible;
                nextButton.IsEnabled = true;
                nextButton.Visibility = Visibility.Visible;
                scrollViewer.IsEnabled = true;
                progressRing.IsActive = false;
            }
        }

        // THEME BUTTON
        private void themeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Theme nextTheme = ThemeHelper.GetNextTheme((Theme)Enum.Parse(typeof(Theme), themeAppBarButton.Label));

            ThemeHelper.UpdateThemeButtons(themeAppBarButton, nextTheme);

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            ThemeHelper.SetTheme(titleBar, this, nextTheme);
        }

        // FULL SCREEN BUTTON
        // TO DO: Simplify or move the below two methods.
        private void fullScreenAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();

            Debug.WriteLine("Currently windowed, trying full screen...");

            bool isFullScreen = view.TryEnterFullScreenMode();

            if (isFullScreen)
            {
                Debug.WriteLine("Entered full screen mode.");                

                Debug.WriteLine("Hiding command bar...");
                commandBar.IsOpen = false;
                commandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Hidden;
                leftCommandBar.IsOpen = false;
                leftCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Hidden;
                imageCommandBar.IsOpen = false;
                imageCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Hidden;

                Debug.WriteLine("Hiding status bar..."); // if not already...
                statusGrid.Visibility = Visibility.Collapsed;

                // Annoyingly, this is firing FIVE times rather than once, which is what you'd expect given the event is set AFTER the window supposedly entered full screen.
                // Presumably this is due to the built in animation. A work aroudn is included in the event fired below.
                Debug.WriteLine("Subscribing to Windows Size Change Event so we know when full screen is exited...");                
                Window.Current.SizeChanged += this.Window_SizeChanged;                
            }
            else
            {                
                statusTextBlock.Text = "Unable to place in full screen mode.";
            }            
        }

        /// <summary>
        /// This is used to put things back to normal after going from Full Screen back to a Window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            Debug.WriteLine("Window Size Changed fired...");

            var displayInformation = DisplayInformation.GetForCurrentView();
            var applicationView = ApplicationView.GetForCurrentView();

            // Due to this event being fired multiple times when returning to Windowed mode,
            // We do not action anything until it matches the target size, that is the full screen resolution of the monitor.
            // We then flag this as a bool so we know when to turn the toolbar back on if this event fires.
            double screenWidth = displayInformation.ScreenWidthInRawPixels;
            double screenHeight = displayInformation.ScreenHeightInRawPixels;
            Debug.WriteLine("Screen width & height: " + screenWidth + " x " + screenHeight);

            double windowWidth = e.Size.Width;
            double windowHeight = e.Size.Height;
            Debug.WriteLine("Window width & height: " + windowWidth + " x " + windowHeight);

            //if (!isFullScreen)
            //{
            //    if (screenWidth != windowWidth || screenHeight != windowHeight) // Window hasn't finished maximising, return...
            //    {
            //        return;
            //    }
            //    else // Window has finished, set full screen to true so above check is skipped next event firing.
            //    {
            //        isFullScreen = true;
            //    }
            //}
            //else // Was FULL screen as of last check, user has clicked Window button and it is starting to resize.
            //{
                //isFullScreen = false;
            if (!applicationView.IsFullScreenMode)
            { 
                Debug.WriteLine("Was full screen, not anymore... cleaning up...");

                Debug.WriteLine("Unsubscribing this event...");
                Window.Current.SizeChanged -= this.Window_SizeChanged;

                Debug.WriteLine("Reopening command bar...");
                commandBar.IsOpen = true;
                commandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
                leftCommandBar.IsOpen = true;
                leftCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
                imageCommandBar.IsOpen = true;
                imageCommandBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;

                Debug.WriteLine("Syncing status bar...");
                statusGrid.Visibility = SettingsHelper.GetStatusBarVisibility(localSettings);

                return;
            }
        }

        // TO DO: Combine the below method/s with a next/previous enum or bool?
        // PREVIOUS BUTTON
        private async void previousButton_Click(object sender, RoutedEventArgs e)
        {
            setBusy(true);

            bool previousSuccess = await handlePreviousAsync();

            if (previousSuccess)
            {
                details.SetImageDetails(file);
                ImageType type = FileHelper.GetImageType(details.Type);

                bool openSuccess = await handleOpenAsync(type);
                if (openSuccess)
                {
                    resetRotation();

                    setStatusText();
                }
                else
                {
                    statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                }

            }
            else
            {
                statusTextBlock.Text = "ERROR: " + lastErrorMessage;
            }

            setBusy(false);
        }

        /// <summary>
        /// Gets the previous image file in a Directory and sets it as the current file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> handlePreviousAsync()
        {
            bool success = false;

            if (imageFiles == null) // The user hasn't pressed Next at least once or there was an error
            {
                imageFiles = await FileHelper.GetImageFilesAsync(file);
            }
            else // The user has pressed next however the directory path has changed due to opening a file from somewhere else
            {
                if (Path.GetDirectoryName(imageFiles.Last().Path) != Path.GetDirectoryName(file.Path))
                {
                    imageFiles = await FileHelper.GetImageFilesAsync(file);
                }
            }

            file = FileHelper.GetPreviousImage(imageFiles, file);
            if (file == null)
            {
                lastErrorMessage = FileHelper.LastErrorMessage;

                return success;
            }
            else
            {
                success = true;
            }

            return success;
        }

        // NEXT BUTTON
        private async void nextButton_Click(object sender, RoutedEventArgs e)
        {
            setBusy(true);

            bool nextSuccess = await handleNextAsync();

            if (nextSuccess)
            {
                file = nextFile;
                nextFile = null;                

                details.SetImageDetails(file);
                ImageType type = FileHelper.GetImageType(details.Type);

                bool openSuccess = await handleOpenAsync(type);
                if (openSuccess)
                {
                    resetRotation();

                    setStatusText();
                }
                else
                {
                    statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                }

            }
            else
            {
                statusTextBlock.Text = "ERROR: " + lastErrorMessage;
            }

            //deleteAppBarButton.IsEnabled = true;
            //fullScreenAppBarButton.IsEnabled = true;
            setBusy(false);
        }

        /// <summary>
        /// Gets the next image file in a Directory and sets it as the current file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> handleNextAsync()
        {
            bool success = false;

            if (imageFiles == null) // The user hasn't pressed Next at least once or there was an error
            {
                imageFiles = await FileHelper.GetImageFilesAsync(file);
            }
            else // The user has pressed next however the directory path has changed due to opening a file from somewhere else
            {
                if (Path.GetDirectoryName(imageFiles.First().Path) != Path.GetDirectoryName(file.Path))
                {
                    imageFiles = await FileHelper.GetImageFilesAsync(file);
                }
            }
        
            if (imageFiles == null) // If it's still null after the above there was an error
            {
                lastErrorMessage = FileHelper.LastErrorMessage;

                return success;
            }

            nextFile = FileHelper.GetNextImage(imageFiles, file);
            if (nextFile == null)
            {
                lastErrorMessage = FileHelper.LastErrorMessage;

                return success;
            }
            else
            {
                success = true;
            }

            return success;
        }

        /// <summary>
        /// This tries to keep the garbage colletor from deleting the file reference.
        /// I've found this happens (rarely) but there is nothing in my code as to why.
        /// Possibly using binding to file properties could alleviate this.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            GC.KeepAlive(file);
        }

        // ROTATE RIGTH BUTTON
        private void rotateRightAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            handleRotateLeft_Right(90);
        }

        // ROTATE LEFT BUTTON
        private void rotateLeftAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            handleRotateLeft_Right(-90);
        }

        private void enableRotateButtons(bool enable)
        {
            rotateLeftAppBarButton.IsEnabled = enable;
            rotateRightAppBarButton.IsEnabled = enable;
        }

        /// <summary>
        /// Handles the logic to retate left/right by 90 degrees.
        /// </summary>
        /// <param name="rotationDegrees"></param>
        private void handleRotateLeft_Right(int rotationDegrees)
        {
            if (disableAnimation)
            {
                rotateAnimation.Duration = new Duration(TimeSpan.FromSeconds(0));
            }
            else
            {
                rotateAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.33333333));
            }

            double oldAngle = rotateTransform.Angle;
            newAngle = oldAngle + rotationDegrees;
            if (newAngle == 360)
            {
                oldAngle = -90;
                newAngle = 0;
            }
            else if (newAngle == -90)
            {
                oldAngle = 360;
                newAngle = 270;
            }
            // else use existing angle as its within the "bounds" of a normal 0 to 360 single loop of a circle.

            rotateAnimation.From = oldAngle;
            rotateAnimation.To = newAngle;

            //rotationTextBlock.Text = newAngle.ToString() + "°";

            enableRotateButtons(false);
            scrollViewer.MinZoomFactor = 0.1f;
            rotateStoryboard.Begin();            
        }

        /// <summary>
        /// This is part of a hack to move the "next button" slightly left if the vertical scrollbar is currently visable.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="dp"></param>
        private void scrollViewer_ScrollableHeightChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (scrollViewer.ScrollableHeight > 0)
            {
                if (nextButton.Margin.Right != 16)
                {
                    nextButton.Margin = new Thickness(-16, 0, 16, 0);
                }
            }
            else
            {
                if (nextButton.Margin.Right != 0)
                {
                    nextButton.Margin = new Thickness(0, 0, 0, 0);
                }
            }
        }

        // ZOOM TO ACTUAL SIZE BUTTON
        private void zoomToActualSizeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            handleZoomToActualSize();

            SettingsHelper.SetSavedViewMode(localSettings, viewMode);
        }

        /// <summary>
        /// Logic to zoom image to actual pixels.
        /// </summary>
        private void handleZoomToActualSize(double x = 0, double y = 0)
        {            
            //if (scrollViewer.ZoomFactor == 1 && dpiOverrideFraction == 1)
            if (scrollViewer.ZoomFactor * DPIOverrideFraction == 1)
            {
                centerImageOnly();

                return;
            }

            if (x == 0 && y == 0)
            {
                x = (layoutTransform.ActualWidth * DPIOverrideFraction - scrollViewer.ViewportWidth) / 2;
                y = (layoutTransform.ActualHeight * DPIOverrideFraction - scrollViewer.ViewportHeight) / 2;
            }

            Debug.WriteLine(DateTime.Now + "Zooming to 1:1 now...");
            scrollViewer.ChangeView(x, y, (float)DPIOverrideFraction, disableAnimation);
            // Works, but with no animation...
            //scrollViewer.ZoomToFactor(1f);

            // TESTING
            //await Task.Delay(1000);

            //Tuple<double, double> offset = getOffset();
            //scrollViewer.ChangeView(offset.Item1, offset.Item2, null, disableAnimation);
            //scrollViewer.ScrollToHorizontalOffset(offset.Item1);
            //scrollViewer.ScrollToVerticalOffset(offset.Item2);

            zoomTextBlock.Text = "100%";
            displayedSizeTextBlock.Text = layoutTransform.ActualWidth + " x " + layoutTransform.ActualHeight;
            
            viewMode = ViewMode.ActualPixels;
        }

        // This is only used once so maybe I should just move it into the actual method?
        //private Tuple<double, double> getOffset()
        //{
        //    //float x = (float)((layoutTransform.ActualWidth / 2) - (scrollViewer.ViewportWidth / 2));
        //    //float y = (float)((layoutTransform.ActualHeight / 2) - (scrollViewer.ViewportHeight / 2));
        //    //double horizontalOffset = scrollViewer.ScrollableWidth / 2;
        //    //double verticalOffset = scrollViewer.ScrollableHeight / 2;
        //    double horizontalOffset = (layoutTransform.ActualWidth * scrollViewer.ZoomFactor - scrollViewer.ViewportWidth) / 2;
        //    double verticalOffset = (layoutTransform.ActualHeight * scrollViewer.ZoomFactor - scrollViewer.ViewportHeight) / 2;            

        //    return Tuple.Create(horizontalOffset, verticalOffset);
        //}

        /// <summary>
        /// Centers an image.
        /// This is used when the zoom factor hasn't changed but the center coordinates have.
        /// E.g. Actual pixels is already one and opening a new file
        /// or, Rotating an image where it's not set to "Fit" mode.
        /// </summary>
        private void centerImageOnly()
        {
            //Tuple<double, double> offset = getOffset();
            double horizontalOffset = (layoutTransform.ActualWidth * scrollViewer.ZoomFactor - scrollViewer.ViewportWidth) / 2;
            double verticalOffset = (layoutTransform.ActualHeight * scrollViewer.ZoomFactor - scrollViewer.ViewportHeight) / 2;

            // If this is during a rotation animation, we don't actually want to animate the rotation - even if the user has specified this
            // As it results in extreme jerkiness.
            // Likewise if the image has just been loaded. Otherwise it does the centering animation which doesn't make sense/looks bad with a new image.

            ClockState state = rotateStoryboard.GetCurrentState();
            bool animationOverride;
            if (justLoaded || state != ClockState.Stopped)
            {
                animationOverride = true;

                if (justLoaded)
                {
                    justLoaded = false;
                }
            }
            else // Not rotating...
            {
                animationOverride = disableAnimation;
            }    

            //scrollViewer.ChangeView(offset.Item1, offset.Item2, null, animationOverride);
            scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, animationOverride);

        }

        // FIT BUTTON
        private void fitToWindowAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            handleFitToWindow();

            SettingsHelper.SetSavedViewMode(localSettings, viewMode);
        }

        /// <summary>
        /// Fit image to window logic.
        /// Bloody hard to get this working :)
        /// </summary>
        private void handleFitToWindow()
        {
            // Directly after opening, DesiredSize, etc, will be 0, so we can fallback to the actual dimensions of the image if this is the case;
            double imageWidth = layoutTransform.ActualWidth;
            double imageHeight = layoutTransform.ActualHeight;
            
            double widthRatio = scrollViewer.ViewportWidth / imageWidth;
            double heightRatio = scrollViewer.ViewportHeight / imageHeight;

            double smallestRatio;
            if (widthRatio > heightRatio)
            {
                smallestRatio = heightRatio;            
            }
            else
            {
                smallestRatio = widthRatio;              
            }
            
            // We do not want to animate fitting to Window if the user explicity turns off animation.
            // However, even if on, we don't want to animate if the image has just been loaded. Otherwise it "shrinks" into view even though previously no image was open, or another image was open... and just doesn't look right.
            // Furthermore, we don't want to animation if the resize is due to a rotation, as it creates excessive jerkiness due to repeated calls to this method.
            bool animationOverride;

            ClockState state = rotateStoryboard.GetCurrentState();
            System.Diagnostics.Debug.WriteLine("Clockstate: " + state.ToString());
            if (disableAnimation || justLoaded || state == ClockState.Active)
            {
                animationOverride = true;

                if (justLoaded)
                {
                    justLoaded = false;
                }
            }
            else
            {
                animationOverride = false;
            }
            System.Diagnostics.Debug.WriteLine("Animate Override: " + animationOverride.ToString());

            // If we don't set this to zero, e.g. null instead, the animation is a bit jerky due to delayed calucations,
            // Causing the preivous offset to be off.
            scrollViewer.ChangeView(0, 0, (float)smallestRatio, animationOverride);
            
            // We only want to update the Zoom % and displayed size if the animation is over (or there is no animation).
            if (state == ClockState.Filling)
            {
                System.Diagnostics.Debug.WriteLine("Updating Zoom Percent & Displayed Size...");
                zoomTextBlock.Text = Math.Round(smallestRatio / DPIOverrideFraction * 100).ToString() + "%";
                displayedSizeTextBlock.Text = Math.Floor(imageWidth * smallestRatio / DPIOverrideFraction) + " x " + Math.Floor(imageHeight * smallestRatio / DPIOverrideFraction);
            }
            
            viewMode = ViewMode.Fit;
        }

        /// <summary>
        /// This event will fire when a new image has been loaded with different dimensions, or an existing image has been rotated.
        /// In other words, when the size of the image container - the Layout Transfer - changes.
        /// As such, we need to center or resize it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void layoutTransform_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (image.Source == null) { return;  }

            Debug.WriteLine(DateTime.Now + " - Layout Transform Size Changed...");

            centerOrResizeImage();            
        }

        /// <summary>
        /// This event will fire when the scroll viewer has changed size due to the window being resized.
        /// We will need to center or resize the image accordingly.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void scrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (image.Source == null) { return; }

            Debug.WriteLine(DateTime.Now + " - Scroll Viewer Size Changed...");

            centerOrResizeImage();
        }

        /// <summary>
        /// This is used to determine if an image needs to be centered or resized & centered based on the current view mode.
        /// </summary>
        private void centerOrResizeImage()
        {
            if (viewMode == ViewMode.ActualPixels || viewMode == ViewMode.CustomZoom)
            {
                Debug.WriteLine("Centering image...");
                centerImageOnly();
            }
            else if (viewMode == ViewMode.Fit) // Fit
            {
                Debug.WriteLine("Fitting to Window...");
                handleFitToWindow();
            }
            //TO DO: Custom zoom
        }

        // Fired when a user slides the slider thumb.
        private void zoomSlider_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (image.Source == null) { return; }

            e.Handled = true;
                        
            Debug.WriteLine(DateTime.Now + " - Zoom slider Manipulation Delta...");

            handleZoomSliderChanged();
        }

        // Fired when a user click the zoom slider to where they want the zoom factor to go...
        // BUG: This doesn't work if the user clicks on the Zoom Slider "line". But does outside of this. Need to fix.
        private void zoomSlider_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now + " - Zoom slider tapped/clicked...");

            //if (!disableAnimation)
            //{

            //}

            handleZoomSliderChanged();

            //scrollViewer.ViewChanged -= scrollViewer_ViewChanged;
        }

        /// <summary>
        /// Handles the logc when the Zoom Slider value is changed manually by the user.
        /// This was a cunt.
        /// </summary>
        private void handleZoomSliderChanged()
        {
            if (zoomSliderChanged == true) { return; }

            zoomSliderChanged = true;

            double zoomFactor;
            double zoomSliderValue = (float)(zoomSlider.Value);
            if (zoomSliderValue == 50)
            {
                zoomFactor = 1;
            }            
            else if (zoomSliderValue >= 0 && zoomSliderValue < 50)
            {
                // ((x - min) * 100 / (max - min)) (100 [%] is an example... in my case 50 for each side as they are calculated differently.)
                //percentage = ((zoomFactor - 0.2) * 100 / 0.8) * 0.5;
                //Simplified: (zoomFactor - 0.2)/0.8*50
                //zoomFactor = (zoomSliderValue / 50 * 0.8) + 0.2; =>
                zoomFactor = (zoomSliderValue * 0.016) + 0.2;
            }
            else if (zoomSliderValue > 50 && zoomSliderValue <= 100)
            {
                //percentage = 50 + (zoomFactor - 1) / (5 - 1) * 50; // As we want 5 times zoom max.
                //Simplified: 50 + ((zoomFactor - 1) / 4) * 50;
                //zoomFactor = (zoomSliderValue - 50) / 50 * 4 + 1; =>
                zoomFactor = (zoomSliderValue - 50) * 0.08 + 1;
            }
            else // Out of bounds!
            {
                zoomFactor = 1;
            }

            zoomFactor = zoomFactor * DPIOverrideFraction;


            double x = (layoutTransform.ActualWidth * zoomFactor - scrollViewer.ViewportWidth) / 2;
            double y = (layoutTransform.ActualHeight * zoomFactor - scrollViewer.ViewportHeight) / 2;
            scrollViewer.ChangeView(x, y, (float)(zoomFactor), disableAnimation);

            //scrollViewer.ZoomToFactor((float)zoomFactor);
            //scrollViewer.ScrollToHorizontalOffset(scrollViewer.ScrollableWidth / 2);
            //scrollViewer.ScrollToVerticalOffset(scrollViewer.ScrollableHeight / 2);

            zoomTextBlock.Text = (zoomFactor / DPIOverrideFraction * 100).ToString() + "%";
            displayedSizeTextBlock.Text = Math.Round(layoutTransform.ActualWidth / DPIOverrideFraction * zoomFactor) + " x " + Math.Round(layoutTransform.ActualHeight / DPIOverrideFraction * zoomFactor);            

            zoomSliderChanged = false;
        }

        /// <summary>
        /// Fired when a user finishes sliding the Zoom Slider thumb.
        /// Updates the Zoom one last time, and updates the View Mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void zoomSlider_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            Debug.WriteLine(DateTime.Now + " - zoomSlider_ManipulationCompleted...");

            // Run this one last time...
            handleZoomSliderChanged();

            viewMode = ViewMode.CustomZoom;
            // De-register event so it's not unnessessaryily fired once Zoom Slider is no longer in use.
            //scrollViewer.ViewChanged -= scrollViewer_ViewChanged;
        }

        /// <summary>
        /// This is used to update the Zoom Slider value via code so its in sync with the actual Scroll Viewer Zoom Factor.
        /// Originally I had this set via x:Bind in the XAML
        /// However, it was too inflexible - e.g. updating the Zoom Slider motion during a resize animation, causing the circle to move out of sync of the mouse/finger.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void zoomAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            // If already open, do nothing - no need to update as it should now close.
            // If not open - update value. As it will be opened via animation.
            if (zoomAppBarButton.Flyout.IsOpen) { return; }

            // As this used to be used in XAML, we'll re-use it here in code rather then moving it for now - just in case I revert at some point.
            FloatToPercentageDoubleConverter converter = new FloatToPercentageDoubleConverter();

            zoomSlider.Value = (double)converter.Convert(scrollViewer.ZoomFactor / DPIOverrideFraction, Type.GetType("Double"), null, Language);
        }

        /// <summary>
        /// Zooms to 1:1 with clicked position centered (where possible). Fits image if already 1:1.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void image_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (viewMode != ViewMode.Fit)
            {
                handleFitToWindow();
            }
            else //currently 1:1 or custom zoom.
            {                
                Windows.Foundation.Point position = e.GetPosition(layoutTransform);
                double x = position.X * DPIOverrideFraction - scrollViewer.ViewportWidth/2;                
                double y = position.Y * DPIOverrideFraction - scrollViewer.ViewportHeight/2;
                //statusTextBlock.Text = x + ", " + y;
                handleZoomToActualSize(x, y);
            }

            SettingsHelper.SetSavedViewMode(localSettings, viewMode);
        }

        /// <summary>
        /// Handles dragging image to re-center if image size is greater than viewport.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void image_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // TO DO: Also add an return switch if the Zoom is less than the viewable area.
            if (viewMode == ViewMode.Fit) { return; }

            // We do not want the animation enable here, even if the user has it on
            // As the standard usage is the click and drag with the offset following the finger and/or pointer.
            // If you enabled animation there is a huge lag in this as it keeps trying to animation previous drag cooordinates
            // And this isn't a "Click to re-center" method where perhaps that behaviour would be wanted.
            scrollViewer.ChangeView(scrollViewer.HorizontalOffset - e.Delta.Translation.X, scrollViewer.VerticalOffset - e.Delta.Translation.Y, null, true);
        }

        // Not needed?
        private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Debug.WriteLine(scrollViewer.HorizontalOffset + ", " + scrollViewer.VerticalOffset);
        }

        /// <summary>
        /// Opens or closes the settings pane.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void settingsAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        /// <summary>
        /// Hides the status bar, or shows it again.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void statusBarToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (statusGrid == null || !navigatedTo) { return; }

            if (statusBarToggleSwitch.IsOn)
            {
                hideRectangle.Visibility = Visibility.Collapsed;

                SettingsHelper.SetStatusBarVisibility(localSettings, Visibility.Visible);
            }
            else //is off
            {                
                hideRectangle.Visibility = Visibility.Visible;

                SettingsHelper.SetStatusBarVisibility(localSettings, Visibility.Collapsed);
            }            
        }

        /// <summary>
        /// Turns animations on or off when the scroll viewer view is changed, or image container rotated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void animationsToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!navigatedTo) { return; }

            if (animationsToggleSwitch.IsOn)
            {
                disableAnimation = false;
            }
            else //is off
            {
                disableAnimation = true;
            }

            SettingsHelper.SetDisableAnimation(localSettings, disableAnimation);
        }

        /// <summary>
        /// Sets the theme.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (themeRadioButtons.SelectedIndex == -1 || e.AddedItems.Count == 0) { return; }

            //Debug.WriteLine(DateTime.Now + "Radio Buttons Selection Changed...");

            string theme = themeRadioButtons.SelectedItem.ToString();

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;

            if (theme == "Light")
            {
                ThemeHelper.SetTheme(titleBar, this, Theme.Light);
                SettingsHelper.SetTheme(localSettings, Theme.Light);
            }
            else if (theme == "Dark")
            {
                ThemeHelper.SetTheme(titleBar, this, Theme.Dark);
                SettingsHelper.SetTheme(localSettings, Theme.Dark);
            }
            else
            {
                ThemeHelper.SetTheme(titleBar, this, Theme.Default);
                SettingsHelper.SetTheme(localSettings, Theme.Default);
            }
         }

        /// <summary>
        /// This is used for two reasons.
        /// 1) It will hide the status bar when the user closes the Split View, if they've selected to hide it.
        /// We don't do this while it is open as it forces the split view to close due to a size change.
        /// This just doesn't feel right as the user might have additional settings they wish to change, ie this wasn't the last change.
        /// 2) It will make the status grid visible if its been set back on. For the same reason as the above, we don't do this until the pane is closed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void splitView_PaneClosed(SplitView sender, object args)
        {
            if (hideRectangle.Visibility == Visibility.Visible && !statusBarToggleSwitch.IsOn)
            {
                statusGrid.Visibility = Visibility.Collapsed;
            }
            else if (hideRectangle.Visibility == Visibility.Collapsed && statusBarToggleSwitch.IsOn)
            {
                statusGrid.Visibility = Visibility.Visible;
            }
            // else do nothing
        }

        /// <summary>
        /// Adds a folder to the future access list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await handleButtonClick("");
        }

        private async Task handleButtonClick(string startLocation)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            if (startLocation == "")
            {
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            }
            else
            {
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            }

            folderPicker.FileTypeFilter.Add("*");           

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                string token = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(folder);
                statusTextBlock.Text = "Picked folder: " + folder.Name;
                FolderWToken folderWToken = new FolderWToken(folder, token);

                if (folders == null)
                {
                    await AddAllowedFoldersAsync();                    
                }
                folders.Add(folderWToken);
            }
            else
            {
                statusTextBlock.Text = "Operation cancelled.";
            }
        }

        private async void askToAddNewLocatoin(IStorageItem item)
        {
            string path = System.IO.Path.GetDirectoryName(item.Path);

            ContentDialog locationDialog = new ContentDialog()
            {
                Title = "Access to Location Denied",
                Content = "Access to the following location is not currently allowed: \r\n\r\n" + path + "\r\n\r\nDo you wish to add this as an allowed location?",
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel"
            };

            ContentDialogResult result = await locationDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                // TO DO: Better error handling then just null?
                file = item as StorageFile;
                if (file == null)
                {
                    statusTextBlock.Text = "ERROR: " + "This file you opened via File Explorer does not appear to be a valid file.";
                }
                else
                {
                    await handleButtonClick(path);

                    // Now the folder is added, open the original image...

                    await openWrapperAsync();
                }
            }
        }

        // Deletes a folder from the future access list.
        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {            
            Button button = (Button)sender;
            object dataContext = ((Grid)button.Parent).DataContext;
            FolderWToken folderWToken = (FolderWToken)dataContext;
            
            StorageApplicationPermissions.FutureAccessList.Remove(folderWToken.Token);
            folders.Remove(folderWToken);
        }

        /// <summary>
        /// Re-enables the rotation buttons when a rotate animation is complete.
        /// Otherwise, the user can click the rotate button during an existing rotation,
        /// Which causes a non-right angle final angle.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rotateStoryboard_Completed(object sender, object e)
        {
            scrollViewer.MinZoomFactor = 0.2f;

            if (rotateRightAppBarButton.IsEnabled != true || rotateLeftAppBarButton.IsEnabled != true)
            {
                enableRotateButtons(true);
            }

            rotationTextBlock.Text = newAngle.ToString() + "°";
        }

        /// <summary>
        /// Sets the animations and status bar on values to match the current state.
        /// Checks the the radio button corresponding to the current theme.
        /// Adds folders currently in the Future Access List to the List Box.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void splitView_PaneOpening(SplitView sender, object args)
        {
            animationsToggleSwitch.Toggled -= animationsToggleSwitch_Toggled;
            animationsToggleSwitch.IsOn = !disableAnimation;
            animationsToggleSwitch.Toggled += animationsToggleSwitch_Toggled;

            InitialViewMode initialViewMode = SettingsHelper.GetInitialViewMode(localSettings);
            viewModeRadioButtons.SelectionChanged -= viewModeRadioButtons_SelectionChanged;
            viewModeRadioButtons.SelectedIndex = (int)initialViewMode;
            viewModeRadioButtons.SelectionChanged += viewModeRadioButtons_SelectionChanged;

            statusBarToggleSwitch.Toggled -= statusBarToggleSwitch_Toggled;
            if (statusGrid.Visibility == Visibility.Visible)
            {
                statusBarToggleSwitch.IsOn = true;
            }    
            else
            {
                statusBarToggleSwitch.IsOn = false;
            }
            statusBarToggleSwitch.Toggled += statusBarToggleSwitch_Toggled;

            exactPixelsToggleSwitch.Toggled -= exactPixelsToggleSwitch_Toggled;
            if (DPIOverrideFraction == 1)
            {
                exactPixelsToggleSwitch.IsOn = false;
            }
            else
            {
                exactPixelsToggleSwitch.IsOn = true;
            }
            exactPixelsToggleSwitch.Toggled += exactPixelsToggleSwitch_Toggled;

            Theme theme = SettingsHelper.GetTheme(localSettings);
            themeRadioButtons.SelectionChanged -= RadioButtons_SelectionChanged;
            themeRadioButtons.SelectedIndex = (int)theme;
            themeRadioButtons.SelectionChanged += RadioButtons_SelectionChanged;

            await AddAllowedFoldersAsync();
        }

        // ABOUT BUTTON
        private async void aboutAppBarButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            string appVersion = string.Format("Version: {0}.{1}.{2}",
                Windows.ApplicationModel.Package.Current.Id.Version.Major,
                Windows.ApplicationModel.Package.Current.Id.Version.Minor,
                Windows.ApplicationModel.Package.Current.Id.Version.Build);

            versionTextBlock.Text = appVersion;

            // This is to fix a bug where in the latest version of WinUI this is taking up space in the root Grid even when not open.
            aboutContentDialog.Visibility = Visibility.Visible;

            await aboutContentDialog.ShowAsync();

            aboutContentDialog.Visibility = Visibility.Collapsed;
        }

        // FEEDBACK BUTTON
        private async void feedbackButton_Click(object sender, RoutedEventArgs e)
        {
            var launcher = Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.GetDefault();
            await launcher.LaunchAsync();
        }

        // Changes the status bar top border color when de-activated
        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
            {
                // do stuff
                statusGrid.BorderBrush = (Windows.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["SystemControlDisabledBaseMediumLowBrush"];
            }
            else
            {
                // do different stuff
                statusGrid.BorderBrush = new Windows.UI.Xaml.Media.SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
            }
        }

        /// <summary>
        /// Overrides DPI when calculating exact pixels/size view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void exactPixelsToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (!navigatedTo) { return; }

            if (exactPixelsToggleSwitch.IsOn)
            {
                double scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

                DPIOverrideFraction = 1 / scaleFactor;
            }
            else
            {
                DPIOverrideFraction = 1;
            }

            SettingsHelper.SetDPIOverrideFraction(localSettings, DPIOverrideFraction);
        }

        private void viewModeRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (viewModeRadioButtons.SelectedIndex == -1 || e.AddedItems.Count == 0) { return; }
                        
            string viewModeSelection = viewModeRadioButtons.SelectedItem.ToString();

            if (viewModeSelection == "Fit")
            {                
                SettingsHelper.SetInitialViewMode(localSettings, InitialViewMode.Fit);
            }
            else if (viewModeSelection == "Exact Size")
            {                
                SettingsHelper.SetInitialViewMode(localSettings, InitialViewMode.ActualSize);
            }
            else // Last Used
            {
                SettingsHelper.SetInitialViewMode(localSettings, InitialViewMode.LastUsed);
            }
        }
    }
}
