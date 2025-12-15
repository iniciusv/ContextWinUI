using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.UI;

namespace ContextWinUI.Views.Components;

// Classe auxiliar para vincular Nome + Cor na interface
public partial class TagUiWrapper : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private SolidColorBrush backgroundBrush;

	public TagUiWrapper(string tagName)
	{
		Name = tagName;
		UpdateColor();

		// Ouve mudanças globais de cor
		TagColorService.Instance.ColorChanged += (s, tag) =>
		{
			if (tag == Name) UpdateColor();
		};
	}

	public void UpdateColor()
	{
		var color = TagColorService.Instance.GetColorForTag(Name);
		// Garante que a criação do Brush ocorra na thread correta se necessário,
		// mas aqui estamos geralmente na UI thread.
		BackgroundBrush = new SolidColorBrush(color);
	}
}