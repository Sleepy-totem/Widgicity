using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Widgicity
{
    public partial class MainWindow : Window
    {
        private Profile _activeProfile = new();
        private readonly Dictionary<Guid, OverlayWindow> _loadedWindows = new();
        private bool _isSynchronizingUi = false;
        private WidgetSettings? _selectedWidget;
        private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "WidgicityConf.json");
        private Forms.NotifyIcon? _trayIcon;
        private bool _isExiting = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            InitializeTrayIcon();
            this.Closing += MainWindow_Closing;
            this.Title = AppInfo.DisplayTitle;
            this.Activated += (s, e) => EnsureAboveWidgets();
            EnsureAboveWidgets();
        }

        public void ShowFromTray()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            EnsureAboveWidgets();
        }

        private void EnsureAboveWidgets()
        {
            this.Topmost = true;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExiting) return;

            e.Cancel = true;
            this.Hide();
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                Visible = true,
                Text = "Widgicity"
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Dashboard", null, (s, e) => ShowFromTray());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ExitApplication()
        {
            _isExiting = true;

            foreach (var win in _loadedWindows.Values)
            {
                win.Close();
            }
            SaveConfiguration();

            _trayIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();

        }

        private void LoadConfiguration()
        {
            if (File.Exists(_storagePath))
            {
                try
                {
                    string rawJson = File.ReadAllText(_storagePath);
                    var parsed = JsonSerializer.Deserialize<Profile>(rawJson);
                    if (parsed != null) _activeProfile = parsed;
                }
                catch { _activeProfile = new Profile(); }
            }

            _activeProfile ??= new Profile();
            if (_activeProfile.Widgets == null) _activeProfile.Widgets = new List<WidgetSettings>();
            RefreshWidgetCollection();
        }

        private void SaveConfiguration()
        {
            string serializedData = JsonSerializer.Serialize(_activeProfile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, serializedData);
        }

        private void RefreshWidgetCollection()
        {
            WidgetListBox.ItemsSource = null;
            WidgetListBox.ItemsSource = _activeProfile.Widgets;
            SyncOverlayWindowsLifecycle();
        }

        private bool IsUrlValid(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return Uri.TryCreate(url, UriKind.Absolute, out var candidate) &&
                   (candidate.Scheme == Uri.UriSchemeHttp || candidate.Scheme == Uri.UriSchemeHttps);
        }

        private void SyncOverlayWindowsLifecycle()
        {
            List<Guid> toRemove = new List<Guid>();
            foreach (var key in _loadedWindows.Keys)
            {
                var match = _activeProfile.Widgets?.Find(w => w.Id == key);
                if (match == null || !match.IsEnabled || !IsUrlValid(match.Url))
                    toRemove.Add(key);
            }

            foreach (var id in toRemove)
            {
                _loadedWindows[id].Close();
                _loadedWindows.Remove(id);
            }

            if (_activeProfile.Widgets != null)
            {
                foreach (var widget in _activeProfile.Widgets)
                {
                    if (widget.IsEnabled && IsUrlValid(widget.Url) && !_loadedWindows.ContainsKey(widget.Id))
                    {
                        var oWin = new OverlayWindow(widget);
                        oWin.Loaded += (s, e) => EnsureAboveWidgets();
                        _loadedWindows.Add(widget.Id, oWin);
                        oWin.Show();
                    }
                }
            }

            EnsureAboveWidgets();
        }

        private void WidgetListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_selectedWidget != null)
            {
                _selectedWidget.PropertyChanged -= SelectedWidget_PropertyChanged;
            }

            if (WidgetListBox.SelectedItem is WidgetSettings widget)
            {
                _selectedWidget = widget;
                _selectedWidget.PropertyChanged += SelectedWidget_PropertyChanged;

                _isSynchronizingUi = true;
                PropertiesPanel.Visibility = Visibility.Visible;

                TxtName.Text = widget.Name;
                TxtUrl.Text = widget.Url;
                TxtX.Text = widget.X.ToString();
                TxtY.Text = widget.Y.ToString();
                TxtWidth.Text = widget.Width.ToString();
                TxtHeight.Text = widget.Height.ToString();
                SldOpacity.Value = widget.Opacity;
                SldZoom.Value = widget.Zoom;
                ChkEnabled.IsChecked = widget.IsEnabled;
                ChkLockAndClickThrough.IsChecked = widget.IsLocked;

                _isSynchronizingUi = false;
            }
            else
            {
                _selectedWidget = null;
                PropertiesPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SelectedWidget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSynchronizingUi || _selectedWidget == null) return;

            if (e.PropertyName == nameof(WidgetSettings.X) ||
                e.PropertyName == nameof(WidgetSettings.Y) ||
                e.PropertyName == nameof(WidgetSettings.Width) ||
                e.PropertyName == nameof(WidgetSettings.Height))
            {
                this.Dispatcher.Invoke(() =>
                {
                    _isSynchronizingUi = true;
                    TxtX.Text = Math.Round(_selectedWidget.X).ToString();
                    TxtY.Text = Math.Round(_selectedWidget.Y).ToString();
                    TxtWidth.Text = Math.Round(_selectedWidget.Width).ToString();
                    TxtHeight.Text = Math.Round(_selectedWidget.Height).ToString();
                    _isSynchronizingUi = false;
                });
            }
        }

        private void SettingControl_Changed(object? sender, EventArgs? e)
        {
            if (_isSynchronizingUi || _selectedWidget == null) return;

            _isSynchronizingUi = true;

            _selectedWidget.Name = TxtName.Text;
            _selectedWidget.Url = TxtUrl.Text;

            if (double.TryParse(TxtX.Text, out double x)) _selectedWidget.X = x;
            if (double.TryParse(TxtY.Text, out double y)) _selectedWidget.Y = y;
            if (double.TryParse(TxtWidth.Text, out double w)) _selectedWidget.Width = w;
            if (double.TryParse(TxtHeight.Text, out double h)) _selectedWidget.Height = h;

            _selectedWidget.Opacity = SldOpacity.Value;
            _selectedWidget.Zoom = SldZoom.Value;
            _selectedWidget.IsEnabled = ChkEnabled.IsChecked ?? false;

            bool combinedLockState = ChkLockAndClickThrough.IsChecked ?? false;
            _selectedWidget.IsLocked = combinedLockState;
            _selectedWidget.IsClickThrough = combinedLockState;

            _isSynchronizingUi = false;

            SyncOverlayWindowsLifecycle();

            if (_loadedWindows.TryGetValue(_selectedWidget.Id, out var win))
            {
                win.UpdateState();
            }
        }

        private void AddWidget_Click(object? sender, RoutedEventArgs e)
        {
            var widget = new WidgetSettings
            {
                Name = $"Widget {(_activeProfile.Widgets?.Count ?? 0) + 1}",
                Url = "",
                X = 100,
                Y = 100,
                Width = 800,
                Height = 600,
                IsClickThrough = false,
                IsLocked = false
            };
            _activeProfile.Widgets?.Add(widget);
            RefreshWidgetCollection();
            WidgetListBox.SelectedItem = widget;
        }

        private void ResetSize_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedWidget == null) return;

            _isSynchronizingUi = true;
            _selectedWidget.X = 100;
            _selectedWidget.Y = 100;
            _selectedWidget.Width = 800;
            _selectedWidget.Height = 600;

            TxtX.Text = "100";
            TxtY.Text = "100";
            TxtWidth.Text = "800";
            TxtHeight.Text = "600";
            _isSynchronizingUi = false;

            SyncOverlayWindowsLifecycle();
            if (_loadedWindows.TryGetValue(_selectedWidget.Id, out var win))
            {
                win.UpdateState();
            }
        }

        private void Duplicate_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedWidget == null) return;

            var clone = new WidgetSettings
            {
                Name = _selectedWidget.Name + " (Copy)",
                Url = _selectedWidget.Url,
                Width = _selectedWidget.Width,
                Height = _selectedWidget.Height,
                Zoom = _selectedWidget.Zoom,
                Opacity = _selectedWidget.Opacity,
                X = _selectedWidget.X + 30,
                Y = _selectedWidget.Y + 30,
                IsClickThrough = _selectedWidget.IsClickThrough,
                IsLocked = _selectedWidget.IsLocked
            };
            _activeProfile.Widgets?.Add(clone);
            RefreshWidgetCollection();
            WidgetListBox.SelectedItem = clone;
        }

        private void Delete_Click(object? sender, RoutedEventArgs e)
        {
            if (_selectedWidget == null) return;

            _activeProfile.Widgets?.Remove(_selectedWidget);
            RefreshWidgetCollection();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            foreach (var win in _loadedWindows.Values)
            {
                win.Close();
            }
            SaveConfiguration();
        }
    }
}