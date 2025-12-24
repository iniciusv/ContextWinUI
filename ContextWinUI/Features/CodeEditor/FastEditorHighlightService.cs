// ARQUIVO: Services/FastEditorHighlightService.cs
using ContextWinUI.Features.CodeEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services
{
	public class FastEditorHighlightService
	{
		public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, bool isDark)
		{
			if (string.IsNullOrWhiteSpace(text)) return new List<HighlightSpan>();

			return await Task.Run(() =>
			{
				var highlights = new List<HighlightSpan>();

				// ParseLexical é muito mais rápido que criar um Workspace.
				// Ele apenas quebra o texto em tokens sem tentar entender o significado.
				var tree = CSharpSyntaxTree.ParseText(text);
				var root = tree.GetRoot();

				// Definição de cores "Hardcoded" para performance máxima no editor
				var commentColor = isDark ? Color.FromArgb(255, 106, 153, 85) : Color.FromArgb(255, 0, 128, 0);
				var stringColor = isDark ? Color.FromArgb(255, 206, 145, 120) : Color.FromArgb(255, 163, 21, 21);
				var keywordColor = isDark ? Color.FromArgb(255, 86, 156, 214) : Color.FromArgb(255, 0, 0, 255);
				var numberColor = isDark ? Color.FromArgb(255, 181, 206, 168) : Colors.Black;

				// 1. Processar "Trivia" (Comentários e Espaços)
				// O Roslyn sabe EXATAMENTE que // dentro de string não é trivia.
				foreach (var trivia in root.DescendantTrivia())
				{
					if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
						trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
					{
						highlights.Add(new HighlightSpan
						{
							Start = trivia.Span.Start,
							Length = trivia.Span.Length,
							Color = commentColor
						});
					}
				}

				// 2. Processar Tokens (Palavras-chave, Strings, Números)
				foreach (var token in root.DescendantTokens())
				{
					var kind = token.Kind();

					if (kind == SyntaxKind.StringLiteralToken ||
						kind == SyntaxKind.InterpolatedStringTextToken)
					{
						highlights.Add(new HighlightSpan
						{
							Start = token.Span.Start,
							Length = token.Span.Length,
							Color = stringColor
						});
					}
					else if (token.IsKeyword())
					{
						highlights.Add(new HighlightSpan
						{
							Start = token.Span.Start,
							Length = token.Span.Length,
							Color = keywordColor
						});
					}
					else if (kind == SyntaxKind.NumericLiteralToken)
					{
						highlights.Add(new HighlightSpan
						{
							Start = token.Span.Start,
							Length = token.Span.Length,
							Color = numberColor
						});
					}
				}

				return highlights;
			});
		}

		public void ApplyHighlights(RichEditBox editor, List<HighlightSpan> highlights)
		{
			if (editor == null || highlights == null) return;

			// Congela a tela para evitar piscar
			editor.Document.BatchDisplayUpdates();
			try
			{
				// Limpa formatação anterior (Crucial para não "perder" o highlight ao apagar texto)
				editor.Document.GetText(TextGetOptions.None, out string text);
				int length = text.Length;

				var defaultColor = ContextWinUI.Helpers.ThemeHelper.IsDarkTheme()
					? Colors.White // Texto padrão branco no dark mode
					: Colors.Black;

				// Reseta tudo para a cor padrão
				editor.Document.GetRange(0, length).CharacterFormat.ForegroundColor = defaultColor;

				// Aplica as cores novas
				foreach (var span in highlights)
				{
					// Proteção contra range inválido (comum durante edição rápida)
					int start = Math.Clamp(span.Start, 0, length);
					int end = Math.Clamp(span.Start + span.Length, 0, length);

					if (end > start)
					{
						editor.Document.GetRange(start, end).CharacterFormat.ForegroundColor = span.Color;
					}
				}
			}
			catch
			{
				// Ignorar erros de concorrência na UI
			}
			finally
			{
				editor.Document.ApplyDisplayUpdates();
			}
		}
	}
}