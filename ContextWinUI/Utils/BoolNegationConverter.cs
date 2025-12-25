// ARQUIVO: Converters/BoolNegationConverter.cs
using Microsoft.UI.Xaml.Data;
using System;

namespace ContextWinUI.Converters;

public class BoolNegationConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is bool booleanValue)
		{
			return !booleanValue;
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		if (value is bool booleanValue)
		{
			return !booleanValue;
		}
		return false;
	}
}