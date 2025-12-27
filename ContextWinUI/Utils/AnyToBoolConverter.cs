using Microsoft.UI.Xaml.Data;
using System;
using System.Collections;
using System.Linq;

namespace ContextWinUI.Converters
{
	public class AnyToBoolConverter : IValueConverter
	{
		public bool Invert { get; set; } = false;
		public bool TreatZeroAsFalse { get; set; } = true;
		public bool TreatEmptyStringAsFalse { get; set; } = true;
		public bool TreatEmptyCollectionAsFalse { get; set; } = true;

		public object Convert(object value, Type targetType, object parameter, string language)
		{
			bool result = ConvertToBool(value);

			if (Invert)
			{
				result = !result;
			}

			return result;
		}

		private bool ConvertToBool(object value)
		{
			if (value == null) return false;

			switch (value)
			{
				case bool b:
					return b;

				case int i:
					return TreatZeroAsFalse ? i > 0 : i != 0;

				case long l:
					return TreatZeroAsFalse ? l > 0 : l != 0;

				case string s:
					if (TreatEmptyStringAsFalse)
						return !string.IsNullOrWhiteSpace(s);
					return !string.IsNullOrEmpty(s);

				case ICollection collection:
					if (TreatEmptyCollectionAsFalse)
						return collection.Count > 0;
					return true;

				case IEnumerable enumerable:
					return enumerable.Cast<object>().Any();

				default:
					return true; // Objeto não nulo = true
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, string language)
		{
			throw new NotImplementedException();
		}
	}
}