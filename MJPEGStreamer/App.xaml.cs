using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace MJPEGStreamer
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    
    {
        Queue<InMemoryRandomAccessStream> _mjpegStreamQueue = new Queue<InMemoryRandomAccessStream>(10);

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;
        }

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            IBackgroundTaskInstance taskInstance = args.TaskInstance;

         /*   Debug.WriteLine("Starting webserver");
            //ImageStreamingServer imageStreamingServer = new ImageStreamingServer();
            StartServer();
            Debug.WriteLine("Started webserver");

            MediaCapture mediaCapture = new MediaCapture();
            ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;


            MediaCaptureInitializationSettings mediaSettings = new MediaCaptureInitializationSettings();
            Object setting = _localSettings.Values["CurrentVideoDeviceId"];
            if (setting == null)
            {
                Debug.WriteLine("ERORR: no webcam configured");
                return;
      
            } 
            mediaSettings.VideoDeviceId = setting.ToString();
            mediaSettings.StreamingCaptureMode = StreamingCaptureMode.Video;
            Debug.WriteLine("VideoDeviceId {0}", setting.ToString());

            DeviceInformationCollection allVideoDevices = DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).GetResults();
            
            foreach (DeviceInformation di in allVideoDevices)
            {
                if (di.Equals(mediaSettings.VideoDeviceId))
                {
                    Debug.WriteLine("Video Device found: {0}", di.Name);
                }
            }


            // Initialize MediaCapture
            try
            {
                mediaCapture.InitializeAsync(mediaSettings).GetResults();
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("The app was denied access to the camera");
            }

            Debug.WriteLine("mediaCapture initialized {0}", mediaSettings.VideoDeviceId);

            var stream = new InMemoryRandomAccessStream();

            while(true)
            {
                mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream).GetResults();

                try
                {
                    if (_mjpegStreamQueue.Count < 10)
                    {
                        _mjpegStreamQueue.Enqueue(stream);
                    }
                    //Debug.WriteLine("Enqueuing steam. Count in queue:" + _mjpegStreamQueue.Count);
                }
                catch (Exception ex)
                {
                    // File I/O errors are reported as exceptions
                    Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
                }
                Task.Delay(1000).Wait();
            }
*/
        }




        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    Debug.WriteLine("Here i should resume again?");
                }

                // Place the frame in the current Window
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // When the navigation stack isn't restored navigate to the first page,
                    // configuring the new page by passing required information as a navigation
                    // parameter
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Ensure the current window is active
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: Save application state and stop any background activity
              Debug.WriteLine("Suspending");
          deferral.Complete();
        }

        private void OnResuming(object sender, object e)
        {
            Debug.WriteLine("Resuming");
        }

    }
}
