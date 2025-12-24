// ARQUIVO: SemanticChangeBlock.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI;

namespace ContextWinUI.Models;

public partial class SemanticChangeBlock : ObservableObject
{
	[ObservableProperty] private bool isSelected = true;

	[ObservableProperty]
	private bool isExpanded = true;

	[ObservableProperty]
	private bool showControls = true;

	[ObservableProperty]
	private string title = string.Empty; // Ex: "Method: AnalyzeContextAsync"

	[ObservableProperty]
	private string symbolIcon = "\uEA86"; // Ícone padrão de código

	public string SymbolId { get; set; } = string.Empty; // ID do nó no grafo

	public ObservableCollection<DiffLine> InternalDiffLines { get; } = new();

	public DiffType MainType { get; set; }

	public SolidColorBrush StatusColor
	{
		get
		{
			if (InternalDiffLines.All(l => l.Type == DiffType.Added))
				return new SolidColorBrush(Color.FromArgb(255, 87, 171, 90)); // Verde
			if (InternalDiffLines.All(l => l.Type == DiffType.Deleted))
				return new SolidColorBrush(Color.FromArgb(255, 229, 83, 75)); // Vermelho

			return new SolidColorBrush(Colors.Orange); // Misto/Modificado
		}
	}

	public SolidColorBrush BlockBackground
	{
		get
		{
			if (MainType == DiffType.Added)
				return new SolidColorBrush(Color.FromArgb(20, 87, 171, 90));
			if (MainType == DiffType.Deleted)
				return new SolidColorBrush(Color.FromArgb(20, 229, 83, 75));

			// Fundo leve para modificações para destacar o bloco semântico
			return new SolidColorBrush(Color.FromArgb(10, 255, 165, 0));
		}
	}

	public SemanticChangeBlock(string title, string icon, DiffType mainType, System.Collections.Generic.IEnumerable<DiffLine> lines)
	{
		Title = title;
		SymbolIcon = icon;
		MainType = mainType;
		foreach (var line in lines)
		{
			InternalDiffLines.Add(line);
		}
	}

}