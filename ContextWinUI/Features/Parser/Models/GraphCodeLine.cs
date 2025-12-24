using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ContextWinUI.Models;

public partial class GraphCodeLine : ObservableObject
{
	public int LineNumber { get; set; }
	public string Text { get; set; } = string.Empty;

	// Nome do símbolo no grafo (ex: "AnalyzeContextAsync")
	public string SymbolName { get; set; } = string.Empty;

	// Tipo do símbolo (Method, Class, etc)
	public string SymbolType { get; set; } = string.Empty;

	// Cor de fundo baseada no nó do grafo
	public SolidColorBrush BackgroundColor { get; set; } = new SolidColorBrush(Colors.Transparent);

	// Cor da borda esquerda para identificar hierarquia
	public SolidColorBrush BorderColor { get; set; } = new SolidColorBrush(Colors.Transparent);
}