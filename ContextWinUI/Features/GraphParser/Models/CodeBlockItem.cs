// ARQUIVO: CodeBlockItem.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace ContextWinUI.Features.GraphParser.Models;

public partial class CodeBlockItem : ObservableObject
{
	public string Id { get; set; } = string.Empty;

	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string content = string.Empty;

	[ObservableProperty]
	private string typeDescription = string.Empty;

	[ObservableProperty]
	private string icon = string.Empty;

	[ObservableProperty]
	private int startLine;

	[ObservableProperty]
	private int endLine;

	public SymbolType SymbolType { get; set; }

	public string FileExtension { get; set; } = ".cs";

	// Propriedade para colorir o cabeçalho do bloco baseado no tipo
	public SolidColorBrush HeaderBrush => SymbolType switch
	{
		SymbolType.Method => new SolidColorBrush(Microsoft.UI.Colors.Goldenrod),
		SymbolType.Class => new SolidColorBrush(Microsoft.UI.Colors.Teal),
		SymbolType.Interface => new SolidColorBrush(Microsoft.UI.Colors.DarkSeaGreen),
		SymbolType.Property => new SolidColorBrush(Microsoft.UI.Colors.SlateGray),
		_ => new SolidColorBrush(Microsoft.UI.Colors.Gray)
	};

	public SegmentType SegmentType { get; set; }

	// Nível de profundidade (0 = raiz, 1 = dentro do namespace, 2 = dentro da classe)
	// Útil para desenhar margem na UI
	public int DepthLevel { get; set; }

	// Helper para saber se é um bloco de "código real" ou apenas estrutura/espaço
	public bool IsStructural => SegmentType == SegmentType.ClassHeader ||
								SegmentType == SegmentType.NamespaceDecl ||
								SegmentType == SegmentType.CloseBrace ||
								SegmentType == SegmentType.Trivia;

	public bool IsGranular => SegmentType == SegmentType.Method ||
							  SegmentType == SegmentType.Property ||
							  SegmentType == SegmentType.Field ||
							  SegmentType == SegmentType.Constructor ||
							  SegmentType == SegmentType.Enum;
}