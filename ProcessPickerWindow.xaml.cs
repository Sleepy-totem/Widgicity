using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace Widgicity
{
    public partial class ProcessPickerWindow : Window
    {
        private class Item
        {
            public string DisplayTitle { get; set; } = "";
            public string ProcessName { get; set; } = "";
        }

        public string? SelectedProcessName { get; private set; }

        public ProcessPickerWindow(List<(string DisplayTitle, string ProcessName)> apps)
        {
            InitializeComponent();
            var items = new List<Item>();
            foreach (var (title, process) in apps)
                items.Add(new Item { DisplayTitle = title, ProcessName = process });
            AppsListBox.ItemsSource = items;
        }

        private void AppsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AppsListBox.SelectedItem is Item item)
            {
                SelectedProcessName = item.ProcessName;
                DialogResult = true;
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}