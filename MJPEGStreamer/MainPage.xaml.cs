using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace MJPEGStreamer
{
    public sealed partial class MainPage : Page
    {
        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        // MediaCapture and its state variables
        private MediaCapture _mediaCapture;
        private int _skipFrameCount;
        private MediaFrameReader _mediaFrameReader;
        private bool _isInitialized;
        private int _videoRotation = (int)VideoRotation.None;

        // UI state
        private bool _isSuspending;
        private bool _isActivePage;
        private bool _isUIActive;
        private Task _setupTask = Task.CompletedTask;

        // Information about the camera device

        ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        private int _currentVideoDeviceIndex = -1;

        private bool taskRunning = false;
        private DeviceInformationCollection _allVideoDevices;
        private InMemoryRandomAccessStream _jpegStreamBuffer;
        private bool _settingsPaneVisible;
        private double _imageQuality = 0.3;
        private bool _cameraComboBoxUserInteraction;
        private bool _mjpegStreamerInitialized;
        private bool _previewVideoEnabled = true;
        private int _httpServerPort = _defaultPort;
        private int _frameRate;
        private ThreadPoolTimer _periodicTimerStreamingStatus;
        private int _skippedFrames;
        private double _sourceFrameRate;
        private int _activityCounter;
        private bool _setupBasedOnState;
        private bool _mjpegStreamerIsInitializing;
        private const int _defaultPort = 8000;

        #region Constructor, lifecycle and navigation

        public MainPage()
        {
            this.InitializeComponent();

            // Do not cache the state of the UI when suspending/navigating
            NavigationCacheMode = NavigationCacheMode.Disabled;

            //MJPEGStreamerInit();

        }

        private void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            Debug.WriteLine("###Suspending###");
            _isSuspending = true;

            var deferral = e.SuspendingOperation.GetDeferral();
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                await SetUpBasedOnStateAsync();
                deferral.Complete();
            });
        }

        private void Application_Resuming(object sender, object o)
        {
            Debug.WriteLine("###Resuming###");
            _isSuspending = false;

            var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                await SetUpBasedOnStateAsync();
            });
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Useful to know when to initialize/clean up the camera
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;
            Window.Current.VisibilityChanged += Window_VisibilityChanged;

            _isActivePage = true;
            await SetUpBasedOnStateAsync();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Handling of this event is included for completenes, as it will only fire when navigating between pages and this sample only includes one page
            Application.Current.Suspending -= Application_Suspending;
            Application.Current.Resuming -= Application_Resuming;
            Window.Current.VisibilityChanged -= Window_VisibilityChanged;

            _isActivePage = false;
            await SetUpBasedOnStateAsync();
        }

        #endregion Constructor, lifecycle and navigation

        private async Task MJPEGStreamerInitAsync()
        {
            Debug.WriteLine("Trying MJPEGStreamerInitAsync");
            if (_mjpegStreamerInitialized || _mjpegStreamerIsInitializing)
            {
                return;
            }
            _mjpegStreamerInitialized = true;
            _mjpegStreamerIsInitializing = true;

            if (_mediaCapture == null)
            {
                _mediaCapture = new MediaCapture();
            }


            Debug.WriteLine("MJPEGStreamerInitAsync: now reading  settings");
            var portSetting = _localSettings.Values["HttpServerPort"];
            if (portSetting != null)
            {
                TextBoxPort.Text = validatePortNumber(portSetting.ToString()).ToString();
                _httpServerPort = (int)portSetting;
            }
            else
            {
                TextBoxPort.Text = _defaultPort.ToString();
                _httpServerPort = _defaultPort;
            }

            var videoRotationInt = _localSettings.Values["VideoRotation"];
            if (videoRotationInt != null)
            {
                _videoRotation = (int)videoRotationInt;
            }

            var imageQualitySingle = _localSettings.Values["ImageQuality"];
            if (imageQualitySingle != null)
            {
                _imageQuality = (double)imageQualitySingle;
            }
            else
            {
                _imageQuality = (double)0.3;
            }
            ImageQualitySlider.Value = _imageQuality * 100;

            var frameRate = _localSettings.Values["FrameRate"];
            if (frameRate != null)
            {
                _frameRate = (int)frameRate;
            }
            else
            {
                _frameRate = (int)5;
            }
            FrameRateSlider.Value = _frameRate;



            var preview = _localSettings.Values["Preview"];
            if (preview != null)
            {
                Debug.WriteLine("preview setting found: " + (bool)preview);
                _previewVideoEnabled = (bool)preview;
            } else
            {
                Debug.WriteLine("no Preview setting found, setting to default ON.");
                _previewVideoEnabled = true;
            }
            PreviewToggleSwitch.IsOn = _previewVideoEnabled;
            UpdatePreviewState();
          

            _allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var id = _localSettings.Values["CurrentVideoDeviceId"];
            if (id == null)
            {
                id = "********no setting*****";
            }

            Debug.WriteLine("_allVideoDevices read");

            _currentVideoDeviceIndex = -1;
            int count = 0;

            CameraComboBox.Items.Clear();
            foreach (DeviceInformation di in _allVideoDevices)
            {
                CameraComboBox.Items.Add(di.Name);

                if (di.Id.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    _currentVideoDeviceIndex = count;
                    CameraComboBox.SelectedIndex = count;
                    Debug.WriteLine("CAMERA INDEX FOUND");
                }
                count++;

                Debug.WriteLine("Video Device: {0} ID:{1}", di.Name, di.Id);
                var mediaFrameSourceGroup = await MediaFrameSourceGroup.FromIdAsync(di.Id);

                foreach (var si in mediaFrameSourceGroup.SourceInfos)
                {
                    Debug.WriteLine("   " + si.MediaStreamType.ToString() + " " + si.SourceKind.ToString());
                }

            }

            ExtendedExecutionSession Session = new ExtendedExecutionSession();
            Session.Reason = ExtendedExecutionReason.Unspecified;
            Session.Description = "Streaming MJPEG";
            Session.Revoked += SessionRevoked;
            ExtendedExecutionResult result = await Session.RequestExtensionAsync();


            var access = await BackgroundExecutionManager.RequestAccessAsync();

            switch (access)
            {
                case BackgroundAccessStatus.Unspecified:
                    Debug.WriteLine("BackgroundAccessStatus unspecified");
                    break;
                case BackgroundAccessStatus.DeniedBySystemPolicy:
                    Debug.WriteLine("BackgroundAccessStatus denied by system policy");
                    break;
                case BackgroundAccessStatus.DeniedByUser:
                    Debug.WriteLine("BackgroundAccessStatus denied by user");
                    break;
                case BackgroundAccessStatus.AlwaysAllowed:
                    Debug.WriteLine("BackgroundAccessStatus always allowed");
                    break;
                case BackgroundAccessStatus.AllowedSubjectToSystemPolicy:
                    Debug.WriteLine("BackgroundAccessStatus allowed subject to policy");
                    break;
                default:
                    Debug.WriteLine("BackgroundAccessStatus something else" + (int)access);
                    break;
            }


            var builder = new BackgroundTaskBuilder();
            builder.Name = "MJPEGBackgroundStreamer";
            builder.IsNetworkRequested = true;
             ApplicationTrigger trigger = new ApplicationTrigger();
             ValueSet valueSet = new ValueSet();
             valueSet.Add("TestValue1", "val1");
             builder.SetTrigger(trigger);
             BackgroundTaskRegistration task = builder.Register();
             Debug.WriteLine("registered builder");
             await trigger.RequestAsync(valueSet);
             Debug.WriteLine("awaited trigger");

            await StartServer();
            _mjpegStreamerIsInitializing = false;

        }

        private void SessionRevoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            Debug.WriteLine("******************Session has been revoked*******************");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ColumnSettings.Width = new GridLength(0);
            UpdateCaptureControls();
        }


        private async void Window_VisibilityChanged(object sender, VisibilityChangedEventArgs args)
        {
            await SetUpBasedOnStateAsync();
        }

        private async void StreamingButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            if (_isServerStarted)
            {
                await StopServer();

            } else
            {
                await StartServer();
            }
        }

        private async void ColorFrameReader_FrameArrivedAsync(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (_skippedFrames < _skipFrameCount)
            {
                _skippedFrames++;
                return;
            }
            _skippedFrames = 0;

            var mediaFrameReference = sender.TryAcquireLatestFrame();
            var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
            var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

            if (softwareBitmap != null)
            {
                var encoderId = BitmapEncoder.JpegEncoderId;

                InMemoryRandomAccessStream jpegStream = new InMemoryRandomAccessStream();

                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(_imageQuality, PropertyType.Single);

                propertySet.Add("ImageQuality", qualityValue);
                BitmapEncoder bitmapEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, jpegStream, propertySet);

                bitmapEncoder.BitmapTransform.Rotation = IntToBitmapRotation(_videoRotation);



                bitmapEncoder.SetSoftwareBitmap(softwareBitmap);
                await bitmapEncoder.FlushAsync();

                
                if (_activityCounter++ > 50)
                {
                    Debug.WriteLine(".");
                    _activityCounter = 0;
                }

                else
                    Debug.Write(".");

                Interlocked.Exchange(ref _jpegStreamBuffer, jpegStream);

                if (_previewVideoEnabled)
                {
                    // Changes to XAML ImageElement must happen on UI thread through Dispatcher
                    var task = imageElement.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        async () =>
                        {
                        // Don't let two copies of this task run at the same time.
                        if (taskRunning)
                            {
                                return;
                            }
                            taskRunning = true;

                            try
                            {
                                InMemoryRandomAccessStream imageElementJpegStream = _jpegStreamBuffer;
                                if (imageElementJpegStream != null)
                                {
                                    BitmapImage bitmapImage = new BitmapImage();
                                    await bitmapImage.SetSourceAsync(imageElementJpegStream);
                                    imageElement.Source = bitmapImage;
                                }
                            } catch (Exception imageElementException)
                            {
                                Debug.WriteLine("Image Element writing exception. " + imageElementException.Message);
                            }

                            taskRunning = false;
                        });
                }
            }
            if (mediaFrameReference != null)
            {
                mediaFrameReference.Dispose();
            }

        }


        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await ShutdownAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }


        private async Task InitializeCameraAsync()
        {

            if (_mediaCapture == null)
            {
                _mediaCapture = new MediaCapture();
            }

            if (_allVideoDevices == null)
                return;

            if (_allVideoDevices.Count == 0)
            {
                Debug.WriteLine("ERROR: no webcam found or available");
                return;
            }

            Object storedVideoDeviceId = _localSettings.Values["CurrentVideoDeviceId"];
            if (storedVideoDeviceId == null)
            {
                storedVideoDeviceId = _allVideoDevices[0].Id;
                _localSettings.Values["CurrentVideoDeviceId"] = storedVideoDeviceId;
                CameraComboBox.SelectedIndex = 0;
                Debug.WriteLine("INFO: no webcam configured. Choosing the first available: " + _allVideoDevices[0].Name);

            }
            else
            {
                Debug.WriteLine("Loaded from Settings - CurrentVideoDeviceId: " + storedVideoDeviceId.ToString());
            }

            var mediaFrameSourceGroup = await MediaFrameSourceGroup.FromIdAsync(storedVideoDeviceId.ToString());

            MediaFrameSourceInfo selectedMediaFrameSourceInfo = null;
            foreach (MediaFrameSourceInfo sourceInfo in mediaFrameSourceGroup.SourceInfos)
            {
                if (sourceInfo.MediaStreamType == MediaStreamType.VideoRecord)
                {
                    selectedMediaFrameSourceInfo = sourceInfo;
                    break;
                }
            }
            if (selectedMediaFrameSourceInfo == null)
            {
                Debug.WriteLine("no compatible MediaSource found.");
                return;
            }

            MediaCaptureInitializationSettings mediaSettings = new MediaCaptureInitializationSettings();
            mediaSettings.VideoDeviceId = storedVideoDeviceId.ToString();
            mediaSettings.SourceGroup = mediaFrameSourceGroup;
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.Video;
            mediaSettings.SharingMode = MediaCaptureSharingMode.ExclusiveControl;
            mediaSettings.MemoryPreference = MediaCaptureMemoryPreference.Cpu;


            try
            {
                await _mediaCapture.InitializeAsync(mediaSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MediaCapture initialization failed: " + ex.Message);
                return;
            }
            Debug.WriteLine("MediaFrameCapture initialized");

            var mediaFrameSource = _mediaCapture.FrameSources[selectedMediaFrameSourceInfo.Id];

            /*var preferredFormat = mediaFrameSource.SupportedFormats.Where(format =>
            {
                return format.VideoFormat.Width >= 1080
                && format.Subtype.Equals(MediaEncodingSubtypes.Yuy2, StringComparison.OrdinalIgnoreCase);

            }).FirstOrDefault();


            var sf = mediaFrameSource.SupportedFormats;

            foreach (MediaFrameFormat mf in sf)
            {
                Debug.WriteLine("MajorType:{0} Subtype:{1} FrameRate:{2} Width:{3} Height:{4}", mf.MajorType, mf.Subtype, mf.FrameRate.Numerator, mf.VideoFormat.Width, mf.VideoFormat.Height);
            }

            if (preferredFormat == null)
            {
                Debug.WriteLine("Preferred Format not supported- EXITING");                // Our desired format is not supported
                return;
            }

           await mediaFrameSource.SetFormatAsync(preferredFormat);
           */

            imageElement.Source = new SoftwareBitmapSource();

            MediaRatio mediaRatio = mediaFrameSource.CurrentFormat.VideoFormat.MediaFrameFormat.FrameRate;
            _sourceFrameRate = (double)mediaRatio.Numerator / (double)mediaRatio.Denominator;
            CalculateFrameRate();
      
            _mediaFrameReader = await _mediaCapture.CreateFrameReaderAsync(mediaFrameSource, MediaEncodingSubtypes.Argb32);
            _mediaFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _mediaFrameReader.FrameArrived += ColorFrameReader_FrameArrivedAsync;
            await _mediaFrameReader.StartAsync();
            Debug.WriteLine("MediaFrameReader StartAsync done ");

            _isInitialized = true;
            UpdateCaptureControls();


        }

        private void CalculateAndSetFrameRate(UInt16 framerate)
        {
            if (framerate < 1 && framerate > 100)
                return;

            _frameRate = framerate;
            CalculateFrameRate();
        }

        private void CalculateFrameRate()
        {
 
            if (_sourceFrameRate < 1)
            {
                _skipFrameCount = 0;
            } else
            {
                _skipFrameCount = (int)(_sourceFrameRate/_frameRate - 0.5);
            }
            
            if (_skipFrameCount > 300 || _skipFrameCount < 0)
            {
                _skipFrameCount = 0;
            }
            Debug.WriteLine("Target framerate: " + _frameRate + " Source framerate: " + _sourceFrameRate + " Skip frames count: " + _skipFrameCount);
        }

          /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task ShutdownAsync()
        {
            Debug.WriteLine("ShutdownAsync: " + new System.Diagnostics.StackTrace().ToString());

            if (_isInitialized)
            {
                // If a recording is in progress during cleanup, stop it 
                if (_mediaFrameReader != null)
                {
                    await _mediaFrameReader.StopAsync();
                    _mediaFrameReader.Dispose();
                }

                _mediaCapture.Dispose();
                _mediaCapture = null;
                _isInitialized = false;
            }
        }



        /// <summary>
        /// Initialize or clean up the camera and our UI,
        /// depending on the page state.
        /// </summary>
        /// <returns></returns>
        private async Task SetUpBasedOnStateAsync()
        {

            Debug.WriteLine("Entering SetupBasedOnStateAsync(): " +  new System.Diagnostics.StackTrace().ToString());
            // Avoid reentrancy: Wait until nobody else is in this function.
            while (!_setupTask.IsCompleted)
            {
                await _setupTask;
            }

            // We want our UI to be active if
            // * We are the current active page.
            // * The window is visible.
            // * The app is not suspending.
            bool previewEnabled = _isActivePage && Window.Current.Visible && !_isSuspending;

            if (_previewVideoEnabled != previewEnabled)
            {
                _previewVideoEnabled = previewEnabled;
                PreviewToggleSwitch.IsOn = previewEnabled;               
            }

            Func<Task> setupAsync = async () =>
            {
                if (_isSuspending)
                {
                    Debug.WriteLine("SetupBasedOnStateAsync - shutting down!");
                    _setupBasedOnState = false;
                    await ShutdownAsync();
                }
                else if (!_setupBasedOnState)
                {
                    Debug.WriteLine("SetupBasedOnStateAsync - setting up!");
                    _setupBasedOnState = false;
                    await MJPEGStreamerInitAsync();
                    await InitializeCameraAsync();

                    // Prevent the device from sleeping while the preview is running
                    _displayRequest.RequestActive();

                }
            };
            Debug.WriteLine("SetupBasedOnStateAsync -calling setup Async!");
            _setupTask = setupAsync();
            Debug.WriteLine("SetupBasedOnStateAsync -setup Async called!");

            await _setupTask;

            Debug.WriteLine("SetupBasedOnStateAsync - awaited setup task!");

            UpdateCaptureControls();

        }

        /// <summary>
        /// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
        /// </summary>
        private void UpdateCaptureControls()
        {
            if (_allVideoDevices == null || _mediaCapture == null)
            {
                RotationButton.IsEnabled = false;
                StreamingButton.IsEnabled = false;
                WebcamButton.IsEnabled = false;
                return;
            }

            RotationButton.IsEnabled = true;
            WebcamButton.IsEnabled = true;

            if (_allVideoDevices.Count > 0)
            {
                StreamingButton.Opacity = 1.0;
                StreamingButton.IsEnabled = true;
                //StreamingButton.Foreground = new SolidColorBrush(Colors.LightGray);
            }
            else
            {
                StreamingButton.IsEnabled = false;
                StreamingButton.Opacity = 0.6;
            }

            NoWebCamErrorIcon.Visibility = _allVideoDevices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;


        }


        private void SettingsButton_Clicked(object sender, RoutedEventArgs e)
        {
        
            _settingsPaneVisible = !_settingsPaneVisible;
            if (_settingsPaneVisible)
            {
                ColumnSettings.Width = new GridLength(220);
            }
            else
            {
                ColumnSettings.Width = new GridLength(0);
            }


        }

        private async void WebcamButton_ClickedAsync(object sender, RoutedEventArgs e)
        {
            if (_allVideoDevices == null)
            {
                return;
            }

            if (_allVideoDevices.Count == 0)
            {
                _currentVideoDeviceIndex = -1;
                return;
            }

            _currentVideoDeviceIndex += 1;
            if (_currentVideoDeviceIndex >= _allVideoDevices.Count)
            {
                _currentVideoDeviceIndex = 0;
            }
            SwitchCamera(_currentVideoDeviceIndex);

        }

        private async void SwitchCamera(int cameraIndex)
        {
            if (_allVideoDevices == null || _allVideoDevices.Count == 0)
            {
                return;
            }

            if (cameraIndex < 0 || cameraIndex >= _allVideoDevices.Count)
            {
                return;
            }

            _localSettings.Values["CurrentVideoDeviceId"] = _allVideoDevices[cameraIndex].Id;
            _currentVideoDeviceIndex = cameraIndex;
            CameraComboBox.SelectedIndex = cameraIndex;

            Debug.WriteLine("Switched to device ID " + _allVideoDevices[cameraIndex].Id);

            await ShutdownAsync();
            await InitializeCameraAsync();
        }

        private void RotationButton_Clicked(object sender, RoutedEventArgs e)
        {
            if (_mediaCapture == null)
            {
                RotationButton.IsEnabled = false;
                return;
            }
            _videoRotation += 1;
            if (_videoRotation > 3)
            {
                _videoRotation = 0;
            }

            _localSettings.Values["VideoRotation"] = _videoRotation;
        }

        public BitmapRotation IntToBitmapRotation(int videoRotation)
        {
            switch (_videoRotation)
            {
                case 0: return BitmapRotation.None;
                case 1: return BitmapRotation.Clockwise90Degrees;
                case 2: return BitmapRotation.Clockwise180Degrees;
                case 3: return BitmapRotation.Clockwise270Degrees;
                default: return BitmapRotation.None;
            }
        }

        private async void TextBoxPort_LostFocusAsync(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("TextBoxPort: " + TextBoxPort.Text);
            int port = validatePortNumber(TextBoxPort.Text);
            if (port != _httpServerPort)
            {
                _httpServerPort = port;
                await StopServer();
                await StartServer();
                _localSettings.Values["HttpServerPort"] = _httpServerPort;
                TextBoxPort.Text = port.ToString();
            }


  
        }

        private int validatePortNumber(String portString)
        {
            ushort port = _defaultPort;
            if (portString != null)
                UInt16.TryParse(portString, out port);
            return validatePortNumber(port);
        }

        private int validatePortNumber(int port)
        {
            if (port >= 80 && port < 65536)
            {
                return port;
            }
            return _defaultPort;
        }

        private void ImageQualitySlider_LostFocus(object sender, RoutedEventArgs e)
        {
            _imageQuality = ImageQualitySlider.Value / 100.0;
            _localSettings.Values["ImageQuality"] = _imageQuality;
        }

        private void ImageQualitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_imageQuality == ImageQualitySlider.Value)
                return;
            _imageQuality = ImageQualitySlider.Value / 100.0;
        }

        private void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_cameraComboBoxUserInteraction)
                return;

            // reset the flag;
            _cameraComboBoxUserInteraction = false;

            int index = CameraComboBox.SelectedIndex;
            Debug.WriteLine("SelectionChange Event " + index.ToString());
            if (index >= 0)
            {
                SwitchCamera(index);
                Debug.WriteLine("SelectionChanged to " + CameraComboBox.SelectedItem.ToString());
            }
            else
            {
                Debug.WriteLine("SelectionChange event - no item selected");
            }

        }

        private void CameraComboBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void CameraComboBox_DropDownOpened(object sender, object e)
        {
            _cameraComboBoxUserInteraction = true;
        }

        private async void ImportantNotesButton_ClickedAsync(object sender, RoutedEventArgs e)
        {
            ImportantNotesContentDialog.Visibility = Visibility.Visible;
            await ImportantNotesContentDialog.ShowAsync();
            Debug.WriteLine("Important Notes button clicked");
        }

        private void FrameRateSlider_LostFocus(object sender, RoutedEventArgs e)
        {
            _frameRate = (int)FrameRateSlider.Value;
            _localSettings.Values["FrameRate"] = _frameRate;
        }

        private void FrameRateSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_frameRate == FrameRateSlider.Value)
                return;
            _frameRate = (int)FrameRateSlider.Value;
            CalculateFrameRate();
        }

        private void PreviewToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            UpdatePreviewState();
        }

        private void UpdatePreviewState()
        {
            Debug.WriteLine("UpdatePreviewState called - toggle: " + PreviewToggleSwitch.IsOn);
            if (PreviewToggleSwitch.IsOn)
            {
                _previewVideoEnabled = true;
                _localSettings.Values["Preview"] = _previewVideoEnabled;
                imageElement.Visibility = Visibility.Visible;
                StreamingStatusTextBox.Visibility = Visibility.Collapsed;
                if (_periodicTimerStreamingStatus != null)
                    _periodicTimerStreamingStatus.Cancel();

            }
            else
            {
                _previewVideoEnabled = false;
                _localSettings.Values["Preview"] = _previewVideoEnabled;
                StreamingStatusTextBox.Visibility = Visibility.Visible;
                imageElement.Visibility = Visibility.Collapsed;

                TimeSpan period = TimeSpan.FromSeconds(1);

                _periodicTimerStreamingStatus = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
                {
                    // Update the UI thread by using the UI core dispatcher.
                    //
                    await Dispatcher.RunAsync(CoreDispatcherPriority.High,
                        () =>
                        {
                            StreamingStatusTextBox.Text = "Active streams: " + _activeStreams + "\r\n" + "JPEG size: " + _jpegStreamBuffer?.Size;

                        });

                }, period);

            }

        }

        private void imageElement_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            PreviewToggleSwitch.IsOn = !PreviewToggleSwitch.IsOn;
        }

        private void VideoPaneGrid_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            if (_settingsPaneVisible)
            {
                if (e.OriginalSource == MJPEGStreamerGrid || e.OriginalSource == imageElement || e.OriginalSource == StreamingStatusTextBox)
                {
                    SettingsButton_Clicked(sender, e);
                }
            }
        }
    }
}