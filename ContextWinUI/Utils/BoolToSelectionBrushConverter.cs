using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace ContextWinUI.Utils;

public class BoolToSelectionBrushConverter : IValueConverter
{
	// Cor Padrão (Cinza/Transparente do tema original)
	public Brush DefaultBrush { get; set; } = (Brush)Application.Current.Resources["LayerFillColorAltBrush"];

	// Cor Selecionada (Suave e Escura)
	// A (Alpha) = 50 (aprox. 20% de opacidade) -> Isso é o que dá a suavidade
	// R, G, B = Azul padrão do sistema (ou um azul marinho)
	// Resultado: Um "Midnight Blue" que se mistura com o fundo escuro.
	public Brush SelectedBrush { get; set; } = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));

	public object Convert(object value, Type targetType, object parameter, string language)
	{
		// Se estiver selecionado, retorna o azul suave
		if (value is bool isSelected && isSelected)
		{
			return SelectedBrush;
		}

		// Se não, retorna o padrão
		return DefaultBrush;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		throw new NotImplementedException();
	}
}