// ARQUIVO: SemanticHighlightService.cs
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeEditor;
using Microsoft.UI;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace ContextWinUI.Services;

public class SemanticHighlightService
{
	// Paletas de cores para Métodos (para variar visualmente)
	private readonly Color[] _darkMethodPalette = new[]
	{
		Color.FromArgb(255, 45, 60, 50),   // Verde escuro
        Color.FromArgb(255, 60, 40, 60),   // Roxo escuro
        Color.FromArgb(255, 60, 50, 40),   // Laranja escuro
        Color.FromArgb(255, 40, 55, 65),   // Azul petróleo
        Color.FromArgb(255, 60, 35, 35)    // Vermelho escuro
    };

	private readonly Color[] _lightMethodPalette = new[]
	{
		Color.FromArgb(255, 225, 255, 225), // Verde pastel
        Color.FromArgb(255, 245, 230, 255), // Roxo pastel
        Color.FromArgb(255, 255, 240, 220), // Laranja pastel
        Color.FromArgb(255, 225, 245, 255), // Azul pastel
        Color.FromArgb(255, 255, 225, 225)  // Vermelho pastel
    };

	public List<HighlightSpan> CalculateContextHighlights(IEnumerable<SymbolNode> symbols, bool isDark)
	{
		var highlights = new List<HighlightSpan>();
		if (symbols == null) return highlights;

		// Cor fixa para Classes (Geralmente englobam tudo, então uma cor neutra é melhor)
		var classColor = isDark
			? Color.FromArgb(255, 20, 25, 35)    // Azul/Cinza muito escuro
			: Color.FromArgb(255, 240, 245, 255); // Branco/Azul gelo

		foreach (var symbol in symbols)
		{
			Color targetColor = Colors.Transparent;

			switch (symbol.Type)
			{
				case SymbolType.Class:
				case SymbolType.Interface:
					targetColor = classColor;
					break;

				case SymbolType.Method:
				case SymbolType.Constructor:
					// Gera um índice baseado no nome do método para escolher uma cor da paleta
					int colorIndex = Math.Abs(symbol.Name.GetHashCode()) % _darkMethodPalette.Length;
					targetColor = isDark
						? _darkMethodPalette[colorIndex]
						: _lightMethodPalette[colorIndex];
					break;

				default:
					continue;
			}

			highlights.Add(new HighlightSpan
			{
				Start = symbol.StartPosition,
				Length = symbol.Length,
				Color = targetColor
			});
		}

		return highlights;
	}
}