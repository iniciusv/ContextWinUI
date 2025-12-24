using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ContextWinUI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; } = false;

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		bool boolValue = value is bool b && b;

		if (Invert) boolValue = !boolValue;

		return boolValue ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}