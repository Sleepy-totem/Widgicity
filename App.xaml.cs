using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Runtime.InteropServices;

namespace Widgicity
{
    public partial class App : System.Windows.Application
    {
        public static CoreWebView2Environment? SharedWebViewEnvironment { get; private set; }

        private const string SingleInstanceMutexName = "Widgicity_SingleInstance_9F2E7B3A";
        private const string ShowRequestedEventName = "Widgicity_ShowRequested_9F2E7B3A";

        private Mutex? _singleInstanceMutex;
        private EventWaitHandle? _showRequestedEvent;

        // Win32 API imports to force the window into the foreground from the tray
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

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
            if (_showRequestedEvent == null) return;

            while (_showRequestedEvent.WaitOne())
            {
                Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var mainWindow = Current.MainWindow as MainWindow;

                    if (mainWindow != null)
                    {
                        // 1. Make sure the window is visible if it was hidden to tray
                        if (!mainWindow.IsVisible)
                        {
                            mainWindow.Show();
                        }

                        // 2. Un-minimize it if it was minimized
                        if (mainWindow.WindowState == WindowState.Minimized)
                        {
                            mainWindow.WindowState = WindowState.Normal;
                        }

                        // 3. Get the native window handle
                        var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                        IntPtr hWnd = helper.Handle;

                        if (hWnd != IntPtr.Zero)
                        {
                            // Force native restore and bring to top bypasses Windows focus stealing restrictions
                            ShowWindow(hWnd, SW_RESTORE);
                            SetForegroundWindow(hWnd);
                        }

                        // 4. Standard WPF activation pass
                        mainWindow.Activate();
                        mainWindow.Topmost = true;  // Temporary push to front
                        mainWindow.Topmost = false; // Reset so it doesn't lock over other apps
                    }
                }));
            }
        }
    }
}