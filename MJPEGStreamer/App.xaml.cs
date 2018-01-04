using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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
using Windows.UI;
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

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.Resuming += OnResuming;
            this.EnteredBackground += OnEnteredBackground;
            this.LeavingBackground += OnLeavingBackground;
        }

        private void OnLeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            Debug.WriteLine("Leaving Background: " + e);
            Deferral deferral = e.GetDeferral();
            deferral.Complete();
        }

        private void OnEnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            Debug.WriteLine("Entering Background: " + e);
            Deferral deferral = e.GetDeferral();
            deferral.Complete();
        }

        // https://blogs.windows.com/buildingapps/2016/06/07/background-activity-with-the-single-process-model/

        protected override void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);
            IBackgroundTaskInstance taskInstance = args.TaskInstance;

            Debug.WriteLine("OnBackgroundActivated Called");
            /*ApplicationTriggerDetails triggerDetails = (ApplicationTriggerDetails)taskInstance.TriggerDetails;
            ValueSet valueSet = triggerDetails?.Arguments;
            
            Debug.WriteLine("Trigger Details class: " + valueSet?.ToString());

            Frame rootFrame = (Frame)Window.Current.Content;
            if (rootFrame == null || rootFrame.Content.GetType() != typeof(MainPage))
            {
                Debug.WriteLine("MainPage missing or invalid");
                return;
            }

            MainPage mainPage = (MainPage)rootFrame.Content;
            mainPage.Foreground = new SolidColorBrush(Colors.Purple);/*

             /*int i = 0;
            while (true)
            {
                if (i++ > 50)
                {
                    Debug.WriteLine(".");
                    i = 0;
                }

                else
                    Debug.Write(".");

                Thread.Sleep(1000);
            }*/

            // Task.Run(async () => { await mainPage.StartServer(); });

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
