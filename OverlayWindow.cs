using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace Widgicity
{
    public class OverlayWindow : Window
    {
        public WidgetSettings Settings { get; private set; }
        private IntPtr _hwnd;
        private readonly WebView2 _browser = new();
        private readonly Border _containerBorder = new(); // Smart dynamic border layer

        public OverlayWindow(WidgetSettings settings)
        {
            Settings = settings;

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.ResizeMode = ResizeMode.NoResize;
            this.BorderThickness = new Thickness(0);
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.UseLayoutRounding = true;

            this.Left = Settings.X;
            this.Top = Settings.Y;
            this.Width = Settings.Width;
            this.Height = Settings.Height;

            BuildInterface();

            this.Loaded += Window_Loaded;
            this.LocationChanged += Window_LocationChanged;
            this.SizeChanged += Window_SizeChanged;

            this.MouseLeftButtonDown += (s, e) =>
            {
                if (!Settings.IsLocked && e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            };
        }

        private void BuildInterface()
        {
            Grid rootGrid = new Grid();

            _browser.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            _browser.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

            // Connect dynamic structural border around layout elements
            _containerBorder.Child = _browser;
            rootGrid.Children.Add(_containerBorder);

            this.Content = rootGrid;
            InitializeBrowserAsync();
        }

        private async void InitializeBrowserAsync()
        {
            string userDataFolder = Path.Combine(Path.GetTempPath(), "Widgicity", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await _browser.EnsureCoreWebView2Async(App.SharedWebViewEnvironment);

            _browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
        const style = document.createElement('style');
        style.textContent = 'html, body { background: transparent !important; background-color: transparent !important; }';
        document.documentElement.appendChild(style);
    ");

            _browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            _browser.CoreWebView2.WebResourceResponseReceived += (s, args) =>
            {
                int statusCode = args.Response.StatusCode;
                if (statusCode >= 400 && statusCode != 403)
                {
                    this.Dispatcher.Invoke(() => this.Visibility = Visibility.Collapsed);
                }
                else
                {
                    this.Dispatcher.Invoke(() => this.Visibility = Visibility.Visible);
                }
            };

            UpdateState();
        }

        public void UpdateState()
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(UpdateState);
                return;
            }

            this.Left = Settings.X;
            this.Top = Settings.Y;
            this.Width = Settings.Width;
            this.Height = Settings.Height;

            // Handle the visual styles safely depending on whether it is locked or editing
            if (Settings.IsLocked)
            {
                // PRODUCTION MODE: Stripped completely clean of background canvas & borders
                this.Background = Brushes.Transparent;
                _containerBorder.Background = Brushes.Transparent;
                _containerBorder.BorderBrush = Brushes.Transparent;
                _containerBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                // SETUP MODE: Show subtle alignment guides to grab/move around easily
                this.Background = new SolidColorBrush(Color.FromArgb(2, 0, 0, 0)); // Minimum alpha to trap click hits
                _containerBorder.Background = new SolidColorBrush(Color.FromArgb(40, 30, 30, 35)); // Tint area box
                _containerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(98, 0, 238)); // Clean deep-purple accent lines
                _containerBorder.BorderThickness = new Thickness(1);
            }

            this.Opacity = Math.Clamp(Settings.Opacity / 100.0, 0.0, 1.0);

            if (_browser.CoreWebView2 != null)
            {
                _browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                if (Uri.TryCreate(Settings.Url, UriKind.Absolute, out var validatedUri))
                {
                    if (_browser.Source != validatedUri) _browser.Source = validatedUri;
                }
                _browser.ZoomFactor = Settings.Zoom / 100.0;
            }

            _browser.IsEnabled = Settings.IsLocked;
            this.Cursor = Settings.IsLocked ? Cursors.Arrow : Cursors.SizeAll;

            UpdateClickThrough();
        }

        private void UpdateClickThrough()
        {
            if (_hwnd == IntPtr.Zero) return;

            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            if (Settings.IsClickThrough && Settings.IsLocked)
            {
                NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED);
            }
            else
            {
                NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle & ~NativeMethods.WS_EX_TRANSPARENT);
            }
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_NOACTIVATE);

            NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            UpdateState();
        }

        private void Window_LocationChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Normal && !Settings.IsLocked)
            {
                Settings.X = this.Left;
                Settings.Y = this.Top;
            }
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == WindowState.Normal && !Settings.IsLocked)
            {
                Settings.Width = this.Width;
                Settings.Height = this.Height;
            }
        }
    }
}