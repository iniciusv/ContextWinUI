using ColorCode.Styling;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeEditor.Highlight;

public class SemanticHighlightService
{
	private readonly SemanticIndexService _indexService;
	private readonly ThemeService _themeService;

	public SemanticHighlightService(SemanticIndexService indexService, ThemeService themeService)
	{
		_indexService = indexService;
		_themeService = themeService;
	}

	public List<HighlightSpan> CalculateHighlights(string text, int baseOffset, StyleDictionary themeStyles)
	{
		var highlights = new List<HighlightSpan>();

		// Regex para identificar palavras
		var matches = Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");

		foreach (Match match in matches)
		{
			string word = match.Value;

			// 1. Pergunta ao IndexService "O que é isso?"
			var type = _indexService.GetSymbolType(word);

			if (type.HasValue)
			{
				// 2. Pergunta ao ThemeService "Qual a cor disso?"
				var color = _themeService.GetColorForSymbol(type.Value, themeStyles);

				if (color != Microsoft.UI.Colors.Transparent)
				{
					highlights.Add(new HighlightSpan
					{
						Start = baseOffset + match.Index,
						Length = match.Length,
						Color = color,
						Type = MapSymbolToSpanType(type.Value)
					});
				}
			}
		}

		return highlights;
	}

	private SpanType MapSymbolToSpanType(SymbolType type)
	{
		return type switch
		{
			SymbolType.Class => SpanType.Class,
			SymbolType.Interface => SpanType.Interface,
			SymbolType.Method => SpanType.Method,
			_ => SpanType.PlainText
		};
	}
}
