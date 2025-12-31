// ARQUIVO: DateTimeToStringConverter.cs
// Localização: ContextWinUI/Features/GraphParser/Converters/DateTimeToStringConverter.cs

using Microsoft.UI.Xaml.Data;
using System;
using Windows.UI.Xaml.Data;

namespace ContextWinUI.Features.GraphParser.Converters
{
	public class DateTimeToStringConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, string language)
		{
			if (value is DateTime dateTime)
			{
				return dateTime.ToString("dd/MM/yyyy HH:mm:ss");
			}
			return string.Empty;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotSupportedException();
		}
	}
}