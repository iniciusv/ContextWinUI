using ContextWinUI.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ContextWinUI.Features.GraphParser.Models;

public static class CodeBlockUiExtensions
{
	// Mapeamento de Ícones (Segoe Fluent Icons / Segoe MDL2 Assets)
	public static string GetIcon(this CodeBlockItem item) => item.SymbolType switch
	{
		SymbolType.Class => "\uEA86",
		SymbolType.Interface => "\uE943",
		SymbolType.Method => "\uEA37",
		SymbolType.Property => "\uEA39",
		SymbolType.Field => "\uEA38",
		SymbolType.Constructor => "\uEA8C",
		SymbolType.Struct => "\uEA86",
		SymbolType.Enum => "\uE8FD",
		SymbolType.ControlFlow => "\uE8A1",
		SymbolType.LocalVariable => "\uE71D",
		SymbolType.StringLiteral => "\uE8C8",
		_ => "\uE82D"
	};

	// Mapeamento de Cores para Cabeçalhos
	public static SolidColorBrush GetHeaderBrush(this CodeBlockItem item)
	{
		var color = item.SymbolType switch
		{
			SymbolType.Class => Colors.Orange,
			SymbolType.Interface => Colors.LightGreen,
			SymbolType.Method => Colors.MediumPurple,
			SymbolType.Property => Colors.CornflowerBlue,
			SymbolType.Constructor => Colors.Gold,
			SymbolType.Field => Colors.CadetBlue,
			SymbolType.ControlFlow => Colors.LightGray,
			_ => Colors.Gray
		};

		return new SolidColorBrush(color);
	}
}