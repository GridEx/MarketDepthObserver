using System;
using System.Globalization;
using System.Windows.Data;

namespace GridEx.MarketDepthObserver.Converters
{
	class PriceConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is double))
			{
				return Binding.DoNothing;
			}

			return ((double)value).ToString("0.00000000", CultureInfo.GetCultureInfo("en-US"));
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}
