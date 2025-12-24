using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ContextWinUI.Converters;

public class NullToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; } = false;

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		bool isNull = value == null;

		// Se Invert = false (padrão): Null -> Collapsed, NotNull -> Visible
		// Se Invert = true: Null -> Visible, NotNull -> Collapsed

		if (Invert)
			return isNull ? Visibility.Visible : Visibility.Collapsed;

		return isNull ? Visibility.Collapsed : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}