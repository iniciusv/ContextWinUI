using ColorCode.Styling;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared; // Para ThemeHelper
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services;

public class HighlightingOrchestrator : IHighlightingOrchestrator
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;

	public HighlightingOrchestrator()
	{
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
	}
	public async Task HighlightEditorAsync(
		RichEditBox editor,
		string text,
		string extension,
		SemanticHighlightService? semanticService,
		bool isDarkTheme,
		StyleDictionary themeStyles,
		CancellationToken token,
		string? originalContent = null)
	{
		if (string.IsNullOrEmpty(text)) return;

		// Inicia as tarefas de calculo em paralelo
		var syntaxTask = _fastEditorService.CalculateHighlightsAsync(text, isDarkTheme);
		
		var diffTask = Task.Run(() =>
		{
			if (!string.IsNullOrEmpty(originalContent) && originalContent != text)
			{
				return GetSemanticDiffSpans(originalContent, text);
			}
			return new List<TextSpan>();
		}, token);

		await Task.WhenAll(syntaxTask, diffTask);

		if (token.IsCancellationRequested) return;

		var syntaxSpans = syntaxTask.Result;
		var diffSpans = diffTask.Result;

		var document = editor.Document;
		document.BatchDisplayUpdates();

		try
		{
			// Resetar formatação base (cor padrão do texto)
			var fullRange = document.GetRange(0, text.Length);
			fullRange.CharacterFormat.ForegroundColor = isDarkTheme ? Colors.White : Colors.Black;
			fullRange.CharacterFormat.BackgroundColor = Colors.Transparent; // Limpa highlights antigos
			fullRange.CharacterFormat.Bold = FormatEffect.Off;

			// Passo 1: Aplicar Cores de Sintaxe (Fast Editor)
			foreach (var span in syntaxSpans)
			{
				if (span.Start + span.Length > text.Length) continue;

				var range = document.GetRange(span.Start, span.Start + span.Length);
				range.CharacterFormat.ForegroundColor = span.Color;
			}

			// Passo 2: Aplicar Highlight de Diff (Modificações)
			// Usamos uma cor de fundo sutil para indicar mudança
			var diffColor = isDarkTheme
				? Color.FromArgb(60, 255, 215, 0)   // Amarelo Ouro transparente (Dark)
				: Color.FromArgb(80, 255, 165, 0);  // Laranja transparente (Light)

			foreach (var diffSpan in diffSpans)
			{
				if (diffSpan.End > text.Length) continue;

				var range = document.GetRange(diffSpan.Start, diffSpan.End);

				// Aplica cor de fundo para destacar a mudança
				range.CharacterFormat.BackgroundColor = diffColor;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao aplicar highlight: {ex.Message}");
		}
		finally
		{
			document.ApplyDisplayUpdates();
		}
	}

	private List<TextSpan> GetSemanticDiffSpans(string oldContent, string newContent)
	{
		// 1. Extrair tokens significativos (ignorando espaços/comentários)
		var oldTokens = GetSignificantTokens(oldContent);
		var newTokens = GetSignificantTokens(newContent);

		// 2. Calcular LCS (Longest Common Subsequence)
		return CalculateLcsDiff(oldTokens, newTokens);
	}

	private List<SyntaxToken> GetSignificantTokens(string code)
	{
		var tree = CSharpSyntaxTree.ParseText(code);
		// DescendantTokens pega tudo, mas filtramos EOF
		// O Roslyn trata 'Trivia' separadamente, então tokens como 'class', 'Nome', '{' 
		// vêm limpos, sem os espaços ao redor.
		return tree.GetRoot()
				   .DescendantTokens()
				   .Where(t => t.Kind() != SyntaxKind.EndOfFileToken)
				   .ToList();
	}

	private List<TextSpan> CalculateLcsDiff(List<SyntaxToken> oldList, List<SyntaxToken> newList)
	{
		int m = oldList.Count;
		int n = newList.Count;

		// Matriz LCS
		int[,] dp = new int[m + 1, n + 1];

		for (int i = 1; i <= m; i++)
		{
			for (int j = 1; j <= n; j++)
			{
				if (IsTokenEqual(oldList[i - 1], newList[j - 1]))
					dp[i, j] = dp[i - 1, j - 1] + 1;
				else
					dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
			}
		}

		var changes = new List<TextSpan>();
		int x = m, y = n;

		while (y > 0)
		{
			if (x > 0 && IsTokenEqual(oldList[x - 1], newList[y - 1]))
			{
				x--;
				y--;
			}
			else if (x > 0 && dp[x - 1, y] >= dp[x, y - 1])
			{
				x--;
			}
			else
			{
				changes.Add(newList[y - 1].Span);
				y--;
			}
		}

		return changes;
	}

	private bool IsTokenEqual(SyntaxToken t1, SyntaxToken t2) => t1.RawKind == t2.RawKind && t1.Text == t2.Text;
}