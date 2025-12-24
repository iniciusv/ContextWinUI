// ARQUIVO: TagUiWrapper.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ContextWinUI.Views.Components; // Ajuste seu namespace se necessário

public partial class TagUiWrapper : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private SolidColorBrush backgroundBrush;

	// NOVA PROPRIEDADE PARA A COR DO TEXTO
	[ObservableProperty]
	private SolidColorBrush foregroundBrush;

	public TagUiWrapper(string tagName)
	{
		Name = tagName;
		UpdateColor();

		// Assina o evento para atualizar dinamicamente
		TagColorService.Instance.ColorChanged += (s, tag) =>
		{
			if (tag == Name) UpdateColor();
		};
	}

	public void UpdateColor()
	{
		var color = TagColorService.Instance.GetColorForTag(Name);
		BackgroundBrush = new SolidColorBrush(color);

		// FÓRMULA DE LUMINÂNCIA (Percepção humana)
		// Se L > 0.5 (ou 128), a cor é clara, então usamos texto preto.
		// Caso contrário, usamos texto branco.
		double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

		if (luminance > 0.6) // Ajustei para 0.6 para garantir contraste melhor em cores médias
		{
			ForegroundBrush = new SolidColorBrush(Colors.Black);
		}
		else
		{
			ForegroundBrush = new SolidColorBrush(Colors.White);
		}
	}
}