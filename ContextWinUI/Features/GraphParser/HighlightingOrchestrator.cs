using ColorCode.Styling;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared; // Para ThemeHelper
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Dispatching; // Necessário para voltar à UI Thread
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls; // Necessário para RichEditBox
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class HighlightingOrchestrator
{
	private readonly FastEditorHighlightService _fastEditorService;
	private readonly RegexHighlightService _regexHighlightService;

	public HighlightingOrchestrator()
	{
		_fastEditorService = new FastEditorHighlightService();
		_regexHighlightService = new RegexHighlightService();
	}

	/// <summary>
	/// Executa o fluxo completo: Calcula (em background) e Aplica (na UI Thread).
	/// Gerencia internamente o switch de threads.
	/// </summary>
	// HighlightingOrchestrator.cs

	public async Task HighlightEditorAsync(
		RichEditBox editor,
		string text,
		string extension,
		SemanticHighlightService? semanticService,
		bool isDark,                 // Recebe pronto
		StyleDictionary themeStyles, // Recebe pronto
		CancellationToken token)
	{
		if (editor == null || string.IsNullOrEmpty(text)) return;

		try
		{
			// Agora o processamento pesado continua sendo em background,
			// mas sem tocar em propriedades de UI proibidas.
			List<HighlightSpan> spans = await Task.Run(async () =>
			{
				return await CalculateSpansInternal(text, extension, isDark, semanticService, themeStyles);
			}, token);

			if (token.IsCancellationRequested) return;

			editor.DispatcherQueue.TryEnqueue(() =>
			{
				if (token.IsCancellationRequested) return;
				ApplyHighlightsToControl(editor, text.Length, spans, isDark);
			});
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro no HighlightingOrchestrator: {ex.Message}");
		}
	}

	private async Task<List<HighlightSpan>> CalculateSpansInternal(
		string text,
		string extension,
		bool isDark,
		SemanticHighlightService? semanticService,
		StyleDictionary themeStyles)
	{
		List<HighlightSpan> finalSpans;

		if (extension == ".cs")
			finalSpans = await _fastEditorService.CalculateHighlightsAsync(text, isDark);
		else
			finalSpans = await _regexHighlightService.CalculateHighlightsAsync(text, extension, themeStyles);

		if (extension == ".cs" && semanticService != null)
		{
			var semanticSpans = semanticService.CalculateHighlights(text, 0, themeStyles);
			MergeHighlights(finalSpans, semanticSpans);
		}

		return finalSpans;
	}

	// --- LÓGICA DE PINTURA (Movida do CodeBehind) ---

	private void ApplyHighlightsToControl(RichEditBox editor, int textLength, List<HighlightSpan> spans, bool isDark)
	{
		try
		{
			// Otimização crucial: BatchDisplayUpdates evita flicker
			editor.Document.BatchDisplayUpdates();

			var reuseRange = editor.Document.GetRange(0, 0);

			// Reseta a cor base
			reuseRange.SetRange(0, textLength);
			reuseRange.CharacterFormat.ForegroundColor = isDark ? Colors.White : Colors.Black;
			// Se for implementar Diff, talvez queira resetar o BackgroundColor aqui também
			// reuseRange.CharacterFormat.BackgroundColor = Colors.Transparent; 

			foreach (var span in spans)
			{
				ApplySpanOptimized(reuseRange, span, textLength);
			}
		}
		finally
		{
			editor.Document.ApplyDisplayUpdates();
		}
	}

	private void ApplySpanOptimized(ITextRange range, HighlightSpan span, int maxLen)
	{
		int safeStart = Math.Clamp(span.Start, 0, maxLen);
		int safeEnd = Math.Clamp(span.Start + span.Length, 0, maxLen);

		if (safeEnd > safeStart)
		{
			range.SetRange(safeStart, safeEnd);

			// Aqui está preparado para o seu futuro DiffViewer:
			// Se o span definir cor de fundo (ex: diff removal/addition), aplicamos.
			// Caso contrário, aplicamos apenas o foreground (syntax highlighting).

			// Exemplo hipotético para o futuro: if (span.IsBackground) ...

			range.CharacterFormat.ForegroundColor = span.Color;
		}
	}

	private void MergeHighlights(List<HighlightSpan> basic, List<HighlightSpan> semantic)
	{
		foreach (var sem in semantic)
		{
			bool hasConflict = basic.Any(b =>
				b.Start <= sem.Start &&
				(b.Start + b.Length) >= (sem.Start + sem.Length) &&
				(b.Type == SpanType.String || b.Type == SpanType.Comment || b.Type == SpanType.Keyword || b.Type == SpanType.Number));

			if (!hasConflict) basic.Add(sem);
		}
	}
}