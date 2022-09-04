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


// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace QuickImg
{
    /// <summary>
    /// This basically contains all of the applicatoin logic. It really needs to be broken down and seperated out at some point.
    /// There is also a lot of annoying hacks that had to be used using booleans, that I'd like to cut back on.
    /// </summary>
    /// 
    public sealed partial class MainPage : Page
    {
        private string lastErrorMessage;

        private StorageFile file; // The image file
        private ImageDetails details; // Stores image file details so they do not have to be reloaded using an async method

        bool isFullScreen; // Hack to store full screen status
                
        private ViewMode viewMode; // The current view mode - Actual Pixels, Best Fit, or Custom Zoom

        ApplicationDataContainer localSettings; // Stores the settings between sessions
        bool disableAnimation; // Use animations or not

        bool navigatedTo; // Stores that all variables are set and app is ready to go
        bool justLoaded; // Hack...
        bool zoomSliderChanged; // Hack..
        double newAngle; // Required to see when rotation animation is completed, in order to re-enable rotation buttons.
        double dpiOverrideFraction; // Used to stop DPI scaling of an image. This had a lot of flow on affects,
        // But gives the user a true pixel by pixel image.

        // List of folders which the app is permitted to access
        public ObservableCollection<FolderWToken> folders;

        /// <summary>
        /// Constructor
        /// 
        /// Sets up inital values for variables that do not load a setting
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();

            navigatedTo = false;

            lastErrorMessage = "";

            details = new ImageDetails();
            
            isFullScreen = false;
            viewMode = ViewMode.Fit;
                        
            justLoaded = false;
            zoomSliderChanged = false;

            // Hack to add padding to the right button if the scrollviewer is visible.
            scrollViewer.RegisterPropertyChangedCallback(ScrollViewer.ScrollableHeightProperty, scrollViewer_ScrollableHeightChanged);

            // This event is a hack to set the color of the status grid
            Window.Current.Activated += Current_Activated;
        }

        /// <summary>
        /// Loads settings
        /// Shows the feedback button if supported
        /// </summary>
        /// <param name="e"></param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            localSettings = ApplicationData.Current.LocalSettings;

            ApplicationViewTitleBar titleBar = ApplicationView.GetForCurrentView().TitleBar;
            ThemeHelper.SetTheme(titleBar, this, SettingsHelper.GetTheme(localSettings));

            disableAnimation = SettingsHelper.GetDisableAnimation(localSettings);
            statusGrid.Visibility = SettingsHelper.GetStatusBarVisibility(localSettings);

            dpiOverrideFraction = SettingsHelper.GetDPIOverrideFraction(localSettings);

            if (Microsoft.Services.Store.Engagement.StoreServicesFeedbackLauncher.IsSupported())
            {
                this.feedbackButton.Visibility = Visibility.Visible;
            }

            navigatedTo = true;
        }

        /// <summary>
        /// Loads allowed folders
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// This tries to create a File from an image file (hopefully) that the user double clicks on via Explorer
        ///  If the file is not an image (e.g. wrong ext.) it will give an error
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
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
                lastErrorMessage = e.Message;
                statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                return;
            }

            await pre_PostLoadAsync();
        }

        /// <summary>
        /// Open Button
        /// Creates a File from the file picker rather than double clicking an image in File Explorer as above
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                await pre_PostLoadAsync();
            }
            else
            {
                statusTextBlock.Text = "Operation cancelled.";
            }
        }

        /// <summary>
        /// This does some pre and post work required before and after actually reading a file into a bitmap
        /// </summary>
        /// <returns></returns>
        private async Task pre_PostLoadAsync()
        {
            setBusy(true);

            image.Source = null;

            resetRotation();

            details.SetImageDetails(file);
            ImageType type = FileHelper.GetImageType(details.Type);

            // This actually loads the bitmap image from the file
            bool success = await handleOpenAsync(type);
            if (success)
            {
                setStatusText();
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
            nextButton.IsEnabled = true;

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
        /// Render's an image file after a File has been created from either Explorer or File Picker
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

            // A hacky boolean whose use becomes apperent later
            justLoaded = true;

            statusTextBlock.Text = ""; // Clear any previous error messages if present, as successful.
                        
            return success;
        }

        /// <summary>
        /// Sets the status text for a newly opened image file.
        /// TO DO: A good candidate for binding via XAML
        /// </summary>
        private void setStatusText()
        {
            nameTextBlock.Text = details.DisplayName;
            typeTextBlock.Text = details.DisplayType;
            dimensionsTextBlock.Text = details.GetDimensions();            
        }

        /// <summary>
        /// DELETE BUTTON
        /// 
        /// This method is quite obvious.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void deleteAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog deleteFileDialog = new ContentDialog
            {
                Title = "Delete image permanently?",
                Content = "If you delete this image, you won't be able to recover it. Do you want to delete it?",                
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel"
            };

            ContentDialogResult result = await deleteFileDialog.ShowAsync();

            // Delete the file if the user clicked the primary button.
            /// Otherwise, do nothing.
            if (result == ContentDialogResult.Primary)
            {
                setBusy(true);
               
                // Delete the file.
                bool success = await handleDeleteAsync();
                
                if (success)
                {
                    statusTextBlock.Text = "Image deleted successfully.";

                    // Perhaps this needs to be moved/combined into a method to be used in inital setting up of the app
                    nameTextBlock.Text = "";
                    typeTextBlock.Text = "N/A";
                    dimensionsTextBlock.Text = "0 x 0";
                    rotationTextBlock.Text = "0°";
                    zoomTextBlock.Text = "100%";
                    displayedSizeTextBlock.Text = "0 x 0";

                    disableImageManipulationButtons();
                }
                else
                {
                    statusTextBlock.Text = "ERROR: " + lastErrorMessage;
                }

                setBusy(false);
            }
            else
            {
                // The user clicked the CloseButton, pressed ESC, Gamepad B, or the system back button.
                // Do nothing.
            }
        }

        /// <summary>
        /// Delete's the currently opened image file.
        /// 
        /// TO DO: Add a keyboard command for using the "Del" key which calls this also, simliar to the above Button
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
                nextButton.IsEnabled = false;
                scrollViewer.IsEnabled = false;
                progressRing.IsActive = true;
            }
            else
            {
                commandBar.IsEnabled = true;
                previousButton.IsEnabled = true;
                nextButton.IsEnabled = true;
                scrollViewer.IsEnabled = true;
                progressRing.IsActive = false;
            }
        }

        /// <summary>
        /// THEME BUTTON
        /// Quickly toggle the theme and save setting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void themeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Theme nextTheme = ThemeHelper.GetNextTheme((Theme)Enum.Parse(typeof(Theme), themeAppBarButton.Label));

            ThemeHelper.UpdateThemeButtons(themeAppBarButton, nextTheme);

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            ThemeHelper.SetTheme(titleBar, this, nextTheme);
        }

        /// <summary>
        /// FULL SCREEN BUTTON
        /// 
        /// TO DO: Simplify or move the below two methods. I am sure there is a better way to do this.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

                // This is firing up to FIVE times rather than once, which is not what you'd expect given the event is set AFTER the window supposedly entered full screen.
                // Presumably this is due to the built in animation. A work around is included in the event fired below.
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

            var displayInformation = Windows.Graphics.Display.DisplayInformation.GetForCurrentView();

            // Due to this event being fired multiple times when returning to Windowed mode,
            // We do not action anything until it matches the target size, that is the full screen resolution of the monitor.
            // We then flag this as a bool so we know when to turn the toolbar back on if this event fires.
            double screenWidth = displayInformation.ScreenWidthInRawPixels;
            double screenHeight = displayInformation.ScreenHeightInRawPixels;
            Debug.WriteLine("Screen width & height: " + screenWidth + " x " + screenHeight);

            double windowWidth = e.Size.Width;
            double windowHeight = e.Size.Height;
            Debug.WriteLine("Window width & height: " + windowWidth + " x " + windowHeight);

            if (!isFullScreen)
            {
                if (screenWidth != windowWidth || screenHeight != windowHeight) // Window hasn't finished maximising, return...
                {
                    return;
                }
                else // Window has finished, set full screen to true so above check is skipped next event firing.
                {
                    isFullScreen = true;
                }
            }
            else // Was FULL screen as of last check, user has clicked Window button and it is starting to resize.
            {
                isFullScreen = false;

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

        /// <summary>
        /// PREVIOUS BUTTON
        /// 
        /// TO DO: Simplify or move the below two methods.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>        
        private async void previousButton_Click(object sender, RoutedEventArgs e)
        {
            setBusy(true);

            bool previousSuccess = await handlePreviousAsync();

            if (previousSuccess)
            {
                resetRotation();

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

            List<StorageFile> imageFiles = await FileHelper.GetImageFilesAsync(file);
            if (imageFiles == null)
            {
                lastErrorMessage = FileHelper.LastErrorMessage;

                return success;
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

        /// <summary>
        /// NEXT BUTTON
        /// 
        /// TO DO: Simplify or move the below two methods.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>  
        private async void nextButton_Click(object sender, RoutedEventArgs e)
        {
            setBusy(true);

            bool nextSuccess = await handleNextAsync();

            if (nextSuccess)
            {
                resetRotation();

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
                statusTextBlock.Text = "ERROR: " + lastErrorMessage;
            }

            setBusy(false);
        }

        /// <summary>
        /// Gets the next image file in a Directory and sets it as the current file.
        /// </summary>
        /// <returns></returns>
        private async Task<bool> handleNextAsync()
        {
            bool success = false;

            List<StorageFile> imageFiles = await FileHelper.GetImageFilesAsync(file);
            if (imageFiles == null)
            {
                lastErrorMessage = FileHelper.LastErrorMessage;

                return success;
            }

            file = FileHelper.GetNextImage(imageFiles, file);
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

        /// <summary>
        /// TO DO: This...
        /// This tries to keep the garbage colletor from deleting the file reference.
        /// I've found this happens (rarely) but there is nothing in my code as to why.
        /// Possibly binding to file properties via XAML could alleviate this.
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
        /// Handles the logic to retate left or right by 90 degrees.
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
                // TO DO: Declare a const variable up top
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

            rotationTextBlock.Text = newAngle.ToString() + "°";

            enableRotateButtons(false);
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
        }

        /// <summary>
        /// Logic to zoom image to actual pixels.
        /// </summary>
        private void handleZoomToActualSize(double x = 0, double y = 0)
        {            
            if (scrollViewer.ZoomFactor == 1 && dpiOverrideFraction == 1)
            {
                centerImageOnly();

                return;
            }

            if (x == 0 && y == 0)
            {
                x = (layoutTransform.ActualWidth - scrollViewer.ViewportWidth) / 2;
                y = (layoutTransform.ActualHeight - scrollViewer.ViewportHeight) / 2;
            }

            Debug.WriteLine(DateTime.Now + "Zooming to 1:1 now...");
            scrollViewer.ChangeView(x, y, (float)dpiOverrideFraction, disableAnimation);

            displayedSizeTextBlock.Text = layoutTransform.ActualWidth + " x " + layoutTransform.ActualHeight;
            
            viewMode = ViewMode.ActualPixels;
        }

        /// <summary>
        /// Centers an image.
        /// This is used when the zoom factor hasn't changed but the center coordinates have.
        /// E.g. Actual pixels is already one and opening a new file
        /// or, Rotating an image where it's not set to "Fit" mode.
        /// </summary>
        private void centerImageOnly()
        {            
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

            scrollViewer.ChangeView(horizontalOffset, verticalOffset, null, animationOverride);
        }

        // FIT BUTTON
        private void fitToWindowAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            handleFitToWindow();
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

            // If we don't set this to zero, e.g. null instead, the animation is a bit jerky due to delayed calucations,
            // Causing the preivous offset to be off.
            scrollViewer.ChangeView(0, 0, (float)smallestRatio, animationOverride);

            displayedSizeTextBlock.Text = Math.Floor(imageWidth * smallestRatio) + " x " + Math.Floor(imageHeight * smallestRatio);

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
            //TO DO: Custom zoom?
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

            handleZoomSliderChanged();
        }

        /// <summary>
        /// Handles the logc when the Zoom Slider value is changed manually by the user.
        /// This took a lot of figuring out!
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
                       
            double x = (layoutTransform.ActualWidth * zoomFactor - scrollViewer.ViewportWidth) / 2;
            double y = (layoutTransform.ActualHeight * zoomFactor - scrollViewer.ViewportHeight) / 2;
            scrollViewer.ChangeView(x, y, (float)zoomFactor, disableAnimation);            

            displayedSizeTextBlock.Text = Math.Round(layoutTransform.ActualWidth * zoomFactor) + " x " + Math.Round(layoutTransform.ActualHeight * zoomFactor);

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

            zoomSlider.Value = (double)converter.Convert(scrollViewer.ZoomFactor, Type.GetType("Double"), null, Language);
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
                double x = position.X - scrollViewer.ViewportWidth/2;
                double y = position.Y - scrollViewer.ViewportHeight/2;
                
                handleZoomToActualSize(x, y);
            }
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
        //private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        //{
        //    Debug.WriteLine(scrollViewer.HorizontalOffset + ", " + scrollViewer.VerticalOffset);
        //}

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
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // Application now has read/write access to all contents in the picked folder
                // (including other sub-folder contents)
                string token = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(folder);                
                statusTextBlock.Text = "Picked folder: " + folder.Name;                
                FolderWToken folderWToken = new FolderWToken(folder, token);
                folders.Add(folderWToken);
            }
            else
            {
                statusTextBlock.Text = "Operation cancelled.";
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
            if (rotateRightAppBarButton.IsEnabled != true || rotateLeftAppBarButton.IsEnabled != true)
            {
                enableRotateButtons(true);
            }
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
            if (dpiOverrideFraction == 1)
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

            await aboutContentDialog.ShowAsync();
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
                statusGrid.BorderBrush = (Windows.UI.Xaml.Media.SolidColorBrush)Application.Current.Resources["SystemControlDisabledBaseMediumLowBrush"];
            }
            else
            {                
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

                dpiOverrideFraction = 1 / scaleFactor;
            }
            else
            {
                dpiOverrideFraction = 1;
            }

            SettingsHelper.SetDPIOverrideFraction(localSettings, dpiOverrideFraction);
        }
    }
}
