using System.Windows;

namespace Widgicity
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string message)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                this.Dispatcher.Invoke(() => SetStatus(message));
                return;
            }

            TxtStatus.Text = message;
        }
    }
}