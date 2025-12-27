// ARQUIVO: BoolToVisibilityConverter.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ContextWinUI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; } = false;

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		bool boolValue = false;

		// Suporte para booleanos
		if (value is bool b)
		{
			boolValue = b;
		}
		// Suporte para inteiros (ex: Count da lista)
		else if (value is int i)
		{
			boolValue = i > 0;
		}

		// Verifica inversão via Propriedade ou via Parameter no XAML
		bool shouldInvert = Invert || (parameter as string == "Invert");

		if (shouldInvert)
		{
			boolValue = !boolValue;
		}

		return boolValue ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}