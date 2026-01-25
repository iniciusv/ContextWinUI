using ContextWinUI.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml; // Required for Application
using Microsoft.UI.Xaml.Media;
using Windows.UI; // Required for Color

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

    // NEW: Function Bindings for dynamic updates
    public static SolidColorBrush GetBackgroundBrush(bool isSelected, bool isReferenced)
    {
        if (isSelected) 
            return new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
        
        if (isReferenced)
            return new SolidColorBrush(Color.FromArgb(50, 0, 100, 0));

        // Default default (using Resource directly or fallback)
        // We can't access App resources easily from static context without Dispatcher or safe check, 
        // but typically Application.Current.Resources works if on UI thread.
        // For safety, let's use Transparent or try to get the resource if possible, or just standard transparent.
        // The user liked the "Standard" look which might be "LayerFillColorAltBrush".
        // Let's try to grab it safely.
        
         try
         {
             if (Application.Current?.Resources["LayerFillColorAltBrush"] is SolidColorBrush brush)
                 return brush;
         }
         catch {} 

         return new SolidColorBrush(Colors.Transparent);
    }
    
    public static SolidColorBrush GetIndicatorBrush(bool isSelected, bool isReferenced)
    {
        if (isSelected) return new SolidColorBrush(Colors.DodgerBlue); // Blue
        if (isReferenced) return new SolidColorBrush(Colors.DarkGreen); // Green
        return new SolidColorBrush(Colors.Transparent);
    }
}