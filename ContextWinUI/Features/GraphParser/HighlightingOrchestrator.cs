using ColorCode.Styling;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared; // Para ThemeHelper
using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Features.CodeEditor.Highlight;
using ContextWinUI.Features.GraphParser;
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

public class HighlightingOrchestrator : IHighlightingOrchestrator
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

	// Em ContextWinUI.Services.HighlightingOrchestrator.cs

	private void ApplyHighlightsToControl(RichEditBox editor, int textLength, List<HighlightSpan> spans, bool isDark)
	{
		if (editor == null || spans == null) return;

		// 1. CAPTURA O ESTADO ORIGINAL DE READONLY
		// O RichEditBox lança UnauthorizedAccessException se tentarmos formatar enquanto estiver travado.
		bool wasReadOnly = editor.IsReadOnly;

		// 2. DESTRAVA TEMPORARIAMENTE
		if (wasReadOnly)
		{
			editor.IsReadOnly = false;
		}

		// 3. CONGELA ATUALIZAÇÕES VISUAIS (Performance)
		// Impede que o controle pisque a cada caractere pintado
		editor.Document.BatchDisplayUpdates();

		try
		{
			var document = editor.Document;

			// (Opcional) Limpeza prévia: Reseta a cor de todo o texto para o padrão do tema
			// Isso evita que cores antigas fiquem "fantasmas" quando o usuário apaga texto.
			// var fullRange = document.GetRange(0, textLength);
			// fullRange.CharacterFormat.ForegroundColor = isDark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;

			foreach (var span in spans)
			{
				// Validação de segurança para não exceder o tamanho do texto
				if (span.Start < 0 || span.Length <= 0 || (span.Start + span.Length) > textLength)
					continue;

				// Obtém o intervalo de texto (Range)
				var range = document.GetRange(span.Start, span.Start + span.Length);

				// APLICA A COR (Isso causava o erro antes)
				range.CharacterFormat.ForegroundColor = span.Color;

				// Se você tiver suporte a Negrito/Itálico na sua struct HighlightSpan:
				// if (span.IsBold) range.CharacterFormat.Bold = Microsoft.UI.Text.FormatEffect.On;
				// if (span.IsItalic) range.CharacterFormat.Italic = Microsoft.UI.Text.FormatEffect.On;
			}
		}
		catch (Exception ex)
		{
			// Log de segurança para não derrubar a aplicação se o range for inválido
			System.Diagnostics.Debug.WriteLine($"[HighlightingOrchestrator] Erro ao aplicar cores: {ex.Message}");
		}
		finally
		{
			// 4. APLICA AS MUDANÇAS VISUAIS
			editor.Document.ApplyDisplayUpdates();

			// 5. RESTAURA O ESTADO READONLY ORIGINAL
			// Isso é crucial para manter a lógica do lado esquerdo (Referência) funcionando corretamente.
			if (wasReadOnly)
			{
				editor.IsReadOnly = true;
			}
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