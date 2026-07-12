using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Widgicity
{
    public partial class App : Application
    {
        public static CoreWebView2Environment? SharedWebViewEnvironment { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
                await System.Threading.Tasks.Task.Delay(2500); // let the user read the error
            }

            splash.SetStatus("Loading your widget configuration…");
            var main = new MainWindow();

            splash.SetStatus("Ready.");
            main.Show();
            splash.Close();
        }
    }
}