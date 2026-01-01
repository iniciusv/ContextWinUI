using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Utils;

public class LineCountToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		if (value is string text)
		{
			// Conta as quebras de linha. 
			// Se o texto é nulo, conta como 0.
			int lineCount = string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;

			// Se for menor que 4 linhas, consideramos "Pequeno"
			bool isSmall = lineCount < 4;

			// Se o parametro for "HideIfSmall", escondemos quando for pequeno
			if (parameter as string == "HideIfSmall")
			{
				return isSmall ? Visibility.Collapsed : Visibility.Visible;
			}
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}