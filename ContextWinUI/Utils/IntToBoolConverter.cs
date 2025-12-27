using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;

namespace ContextWinUI.Converters
{
	public class IntToBoolConverter : IValueConverter
	{
		public bool Invert { get; set; } = false;

		public object Convert(object value, Type targetType, object parameter, string language)
		{
			bool result = false;

			if (value is int intValue)
			{
				result = intValue > 0;
			}
			else if (value is long longValue)
			{
				result = longValue > 0;
			}
			else if (value is ICollection collection)
			{
				result = collection.Count > 0;
			}

			if (Invert)
			{
				result = !result;
			}

			return result;
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}