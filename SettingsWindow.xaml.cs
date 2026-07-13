using System.Windows;

namespace Widgicity
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _workingCopy;

        public AppSettings? Result { get; private set; }

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();

            // Work on a copy so Cancel doesn't mutate the caller's settings
            _workingCopy = new AppSettings
            {
                LaunchAtStartup = current.LaunchAtStartup,
                StartMinimizedToTray = current.StartMinimizedToTray,
                ProcessCheckIntervalSeconds = current.ProcessCheckIntervalSeconds
            };

            ChkLaunchAtStartup.IsChecked = _workingCopy.LaunchAtStartup;
            ChkStartMinimized.IsChecked = _workingCopy.StartMinimizedToTray;
            TxtPollInterval.Text = _workingCopy.ProcessCheckIntervalSeconds.ToString();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _workingCopy.LaunchAtStartup = ChkLaunchAtStartup.IsChecked ?? false;
            _workingCopy.StartMinimizedToTray = ChkStartMinimized.IsChecked ?? false;

            if (int.TryParse(TxtPollInterval.Text, out int interval) && interval >= 1)
                _workingCopy.ProcessCheckIntervalSeconds = interval;

            AppSettings.ApplyLaunchAtStartup(_workingCopy.LaunchAtStartup);

            Result = _workingCopy;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}