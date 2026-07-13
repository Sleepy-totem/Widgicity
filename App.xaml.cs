using System;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Widgicity
{
    public partial class App : System.Windows.Application
    {
        public static CoreWebView2Environment? SharedWebViewEnvironment { get; private set; }

        private const string SingleInstanceMutexName = "Widgicity_SingleInstance_9F2E7B3A";
        private const string ShowRequestedEventName = "Widgicity_ShowRequested_9F2E7B3A";

        private Mutex? _singleInstanceMutex;
        private EventWaitHandle? _showRequestedEvent;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                // Another copy of Widgicity is already running
                try
                {
                    using var existingInstanceEvent = EventWaitHandle.OpenExisting(ShowRequestedEventName);
                    existingInstanceEvent.Set();
                }
                catch
                {
                    // If we can't reach it for some reason, there's nothing more we can do.
                }

                Shutdown();
                return;
            }

            _showRequestedEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowRequestedEventName);
            var listenerThread = new Thread(ListenForShowRequests) { IsBackground = true };
            listenerThread.Start();

            var splash = new SplashWindow();
            splash.Show();

            try
            {
                string root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Widgicity", "WebView2");

                bool alreadyExists = Directory.Exists(root);

                splash.SetStatus(alreadyExists
                    ? $"Loading web engine data: {root}"
                    : $"Creating folder: {root}");

                var options = new CoreWebView2EnvironmentOptions();

                splash.SetStatus("Starting Microsoft Edge WebView2 runtime…");
                SharedWebViewEnvironment = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: root, options: options);

                splash.SetStatus(alreadyExists
                    ? "Web engine ready."
                    : "Web engine initialized for the first time.");
            }
            catch (Exception ex)
            {
                splash.SetStatus($"Web engine failed to start: {ex.Message}");
                await System.Threading.Tasks.Task.Delay(4000); // let the user read the error
            }

            splash.SetStatus("Loading your widget configuration…");
            var main = new MainWindow();

            splash.SetStatus("Ready.");
            main.Show();
            splash.Close();
        }

        private void ListenForShowRequests()
        {
            while (true)
            {
                _showRequestedEvent!.WaitOne();

                Dispatcher.Invoke(() =>
                {
                    if (Current?.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.ShowFromTray();
                    }
                });
            }
        }
    }
}