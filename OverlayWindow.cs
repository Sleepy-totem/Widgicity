using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Net.Http;
using System.Windows.Threading;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        private readonly Border _containerBorder = new();
        private static readonly HttpClient _httpClient = new HttpClient();
        private DispatcherTimer? _updateCheckTimer;
        private string? _lastETag;
        private string? _lastModified;
        private string? _lastContentHash;
        private bool _isCheckingForUpdate = false;

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
            this.Closed += (s, e) => StopUpdatePolling();

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

            if (Settings.IsLocked)
            {
                this.Background = Brushes.Transparent;
                _containerBorder.Background = Brushes.Transparent;
                _containerBorder.BorderBrush = Brushes.Transparent;
                _containerBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                this.Background = new SolidColorBrush(Color.FromArgb(2, 0, 0, 0));
                _containerBorder.Background = new SolidColorBrush(Color.FromArgb(40, 30, 30, 35));
                _containerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(98, 0, 238));
                _containerBorder.BorderThickness = new Thickness(1);
            }

            this.Opacity = Math.Clamp(Settings.Opacity / 100.0, 0.0, 1.0);

            if (_browser.CoreWebView2 != null)
            {
                _browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                if (Uri.TryCreate(Settings.Url, UriKind.Absolute, out var validatedUri))
                {
                    if (_browser.Source != validatedUri)
                    {
                        _browser.Source = validatedUri;
                        _lastETag = null;
                        _lastModified = null;
                        _lastContentHash = null;
                    }
                }
                _browser.ZoomFactor = Settings.Zoom / 100.0;

                StartUpdatePolling();
            }

            _browser.IsEnabled = Settings.IsLocked;
            this.Cursor = Settings.IsLocked ? Cursors.Arrow : Cursors.SizeAll;

            UpdateClickThrough();
        }

        private void StartUpdatePolling()
        {
            if (_updateCheckTimer != null) return;

            _updateCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3) // adjust to taste
            };
            _updateCheckTimer.Tick += async (s, e) => await CheckForContentUpdateAsync();
            _updateCheckTimer.Start();
        }

        private void StopUpdatePolling()
        {
            _updateCheckTimer?.Stop();
            _updateCheckTimer = null;
        }

        private async Task CheckForContentUpdateAsync()
        {
            if (_isCheckingForUpdate) return; // avoid overlapping checks
            if (!Uri.TryCreate(Settings.Url, UriKind.Absolute, out var uri)) return;
            if (_browser.CoreWebView2 == null) return;

            _isCheckingForUpdate = true;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);

                if (!string.IsNullOrEmpty(_lastETag))
                    request.Headers.TryAddWithoutValidation("If-None-Match", _lastETag);
                if (!string.IsNullOrEmpty(_lastModified))
                    request.Headers.TryAddWithoutValidation("If-Modified-Since", _lastModified);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request);
                }
                catch (HttpRequestException)
                {
                    return; // network hiccup, just skip this cycle
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    return; // server confirmed unchanged, nothing to do
                }

                if (!response.IsSuccessStatusCode)
                {
                    return; // don't act on error responses
                }

                string? newETag = response.Headers.ETag?.Tag;
                string? newModified = response.Content.Headers.LastModified?.ToString("R");
                string html = await response.Content.ReadAsStringAsync();
                string newHash = ComputeHash(html);

                bool isFirstCheck = _lastContentHash == null;
                bool contentChanged = newHash != _lastContentHash;

                _lastETag = newETag;
                _lastModified = newModified;
                _lastContentHash = newHash;

                if (!isFirstCheck && contentChanged)
                {
                    await ApplyContentUpdateAsync(html);
                }
            }
            finally
            {
                _isCheckingForUpdate = false;
            }
        }

        private static string ComputeHash(string content)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(bytes);
        }

        private async Task ApplyContentUpdateAsync(string html)
        {
            if (_browser.CoreWebView2 == null) return;

            string? bodyContent = ExtractBodyInnerHtml(html);

            await this.Dispatcher.InvokeAsync(async () =>
            {
                if (_browser.CoreWebView2 == null) return;

                if (bodyContent == null)
                {
                    _browser.CoreWebView2.Reload();
                    return;
                }

                try
                {
                    string script = "document.body.innerHTML = " + JsonSerializer.Serialize(bodyContent) + ";";
                    await _browser.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch
                {
                    _browser.CoreWebView2.Reload();
                }
            });
        }

        private static string? ExtractBodyInnerHtml(string html)
        {
            var match = Regex.Match(
                html,
                "<body[^>]*>(.*)</body>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
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