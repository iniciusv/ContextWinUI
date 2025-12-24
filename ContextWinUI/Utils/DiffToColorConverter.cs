using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;
using ContextWinUI.Services; // Onde está o enum DiffType

namespace ContextWinUI.Converters;

public class DiffToColorConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is DiffType type)
		{
			string mode = parameter as string ?? "Background";
			bool isDark = Application.Current.RequestedTheme == ApplicationTheme.Dark;

			if (mode == "Background")
			{
				return type switch
				{
					// Fundo Verde (Adição) / Vermelho (Remoção) - Translúcido
					DiffType.Added => new SolidColorBrush(isDark ? Color.FromArgb(60, 46, 160, 67) : Color.FromArgb(60, 205, 255, 205)),
					DiffType.Deleted => new SolidColorBrush(isDark ? Color.FromArgb(60, 200, 50, 50) : Color.FromArgb(60, 255, 205, 205)),
					_ => new SolidColorBrush(Colors.Transparent)
				};
			}
			else // Foreground (Texto)
			{
				// Garante contraste: Texto preto se o fundo for claro, senão segue o tema
				if (!isDark && (type == DiffType.Added || type == DiffType.Deleted))
				{
					return new SolidColorBrush(Colors.Black);
				}
				return new SolidColorBrush(isDark ? Colors.White : Colors.Black);
			}
		}
		return new SolidColorBrush(Colors.Transparent);
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}