// Models.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Widgicity
{
    public class WidgetSettings : INotifyPropertyChanged
    {
        private string _name = "New Widget";
        private string _url = ""; // Empty by default; won't render until valid
        private bool _isEnabled = true;
        private double _x = 100;
        private double _y = 100;
        private double _width = 800;   // Defaulting to 800
        private double _height = 600;  // Defaulting to 600
        private double _zoom = 100.0;
        private double _opacity = 100.0;
        private bool _isClickThrough = true; // Enabled by default
        private bool _isLocked = true;       // Locked by default to support click-through

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Url
        {
            get => _url;
            set { _url = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        public double Zoom
        {
            get => _zoom;
            set { _zoom = value; OnPropertyChanged(); }
        }

        public double Opacity
        {
            get => _opacity;
            set { _opacity = value; OnPropertyChanged(); }
        }

        public bool IsClickThrough
        {
            get => _isClickThrough;
            set { _isClickThrough = value; OnPropertyChanged(); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; OnPropertyChanged(); }
        }

        public bool ShowBorderInEditMode { get; set; } = true;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default Profile";
        public List<WidgetSettings> Widgets { get; set; } = new();
    }
}