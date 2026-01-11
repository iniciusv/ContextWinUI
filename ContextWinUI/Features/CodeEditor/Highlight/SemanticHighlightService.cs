using ColorCode.Styling;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using Microsoft.UI;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ContextWinUI.Features.CodeEditor.Highlight;

public class SemanticHighlightService
{
	private readonly SemanticIndexService _indexService;
	private readonly ThemeService _themeService;

	// Lista de palavras que parecem métodos (têm parenteses depois) mas não devem ser pintadas
	private static readonly HashSet<string> _controlKeywords = new()
	{
		"if", "while", "for", "foreach", "switch", "catch", "using", "lock",
		"fixed", "checked", "unchecked", "sizeof", "typeof", "default", "nameof"
	};

	public SemanticHighlightService(SemanticIndexService indexService, ThemeService themeService)
	{
		_indexService = indexService;
		_themeService = themeService;
	}

	public List<HighlightSpan> CalculateHighlights(string text, int baseOffset, StyleDictionary themeStyles)
	{
		var highlights = new List<HighlightSpan>();

		// ---------------------------------------------------------
		// 1. ANÁLISE SEMÂNTICA (O que já existia: consulta o Grafo)
		// ---------------------------------------------------------
		// Nota: Otimizei o Regex para pegar identificadores C# válidos
		var wordMatches = Regex.Matches(text, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");

		// HashSet para controlar posições já pintadas pela semântica, evitando sobreposição
		var handledPositions = new HashSet<int>();

		foreach (Match match in wordMatches)
		{
			string word = match.Value;
			var type = _indexService.GetSymbolType(word);

			if (type.HasValue)
			{
				var color = _themeService.GetColorForSymbol(type.Value, themeStyles);
				if (color != Colors.Transparent)
				{
					highlights.Add(new HighlightSpan
					{
						Start = baseOffset + match.Index,
						Length = match.Length,
						Color = color,
						Type = MapSymbolToSpanType(type.Value)
					});

					// Marca estes índices como tratados
					for (int i = match.Index; i < match.Index + match.Length; i++)
						handledPositions.Add(i);
				}
			}
		}


		var methodMatches = Regex.Matches(text, @"\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(");

		var methodColor = _themeService.GetColorForSymbol(SymbolType.Method, themeStyles);

		foreach (Match match in methodMatches)
		{
			// O Grupo 1 é o nome do método (sem o parêntese)
			var nameGroup = match.Groups[1];

			// Se já foi pintado pelo índice semântico (ex: é uma Classe ou Interface conhecida), ignoramos
			if (handledPositions.Contains(nameGroup.Index)) continue;

			string word = nameGroup.Value;

			// Se for "if", "while", etc, ignoramos
			if (_controlKeywords.Contains(word)) continue;

			// Se for "new Algo(", provavelmente é um construtor. 
			// Se o índice semântico falhou em dizer que é uma classe, 
			// pintar de amarelo (Método) é um fallback aceitável visualmente.

			highlights.Add(new HighlightSpan
			{
				Start = baseOffset + nameGroup.Index,
				Length = nameGroup.Length,
				Color = methodColor,
				Type = SpanType.Method
			});
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
			SymbolType.Struct => SpanType.Class, // Structs geralmente usam cor de classe
			SymbolType.Enum => SpanType.Class,   // Enums geralmente usam cor de classe/enum
			_ => SpanType.PlainText
		};
	}
}