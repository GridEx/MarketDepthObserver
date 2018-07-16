using System;
using System.Globalization;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace GridEx.MarketDepthObserver.Config
{
	/// <summary>
	/// Interaction logic for IPWindow.xaml
	/// </summary>
	public partial class IPWindow : Window
    {
        public IPWindow()
        {
            InitializeComponent();
        }

        public IPWindow(ref ConnectionConfig config)
        {
            this.config = config;
            IP = config.IP;
            Port = config.Port;
            InitializeComponent();
        }

        ConnectionConfig config;
        public IPAddress IP { get; set; }
        public int Port { get; set; }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            config.IP = IP;
            config.Port = Port;
            try
            {
                config.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save config:\n" + ex.Message, "Problem save config", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            DialogResult = true;
        }
    }

    public class IpValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo ci)
        {
            if (IPAddress.TryParse(value as string, out IPAddress IPadress))
            {
                return new ValidationResult(true, null);
            }
            else
            {
                return new ValidationResult(false, "Wrong IP");
            }
        }
    }

    public class PortValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo ci)
        {
            if (int.TryParse(value as string, out int Port))
            {
                if (Port < 0)
                {
                    return new ValidationResult(false, "Port nubmer must be >= 0");
                }
                return new ValidationResult(true, null);
            }
            else
            {
                return new ValidationResult(false, "Wrong Port");
            }
        }
    }

    public class IPConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as IPAddress)?.MapToIPv4().ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (IPAddress.TryParse(value as string, out IPAddress IPadress))
            {
                return IPadress;
            }
            else
            {
                return Binding.DoNothing;
            }
        }
    }
}
