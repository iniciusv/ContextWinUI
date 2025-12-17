using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace ContextWinUI.Services;

public class RoslynHighlightService
{
	private static readonly Regex _interfaceRegex = new Regex(@"^I[A-Z]", RegexOptions.Compiled);

	public struct HighlightSpan
	{
		public int Start;
		public int Length;
		public Color Color;
	}

	// PASSO 1: Cálculo em Background (Seguro, sem UI)
	public async Task<List<HighlightSpan>> CalculateHighlightsAsync(string text, ColorCode.Styling.StyleDictionary theme, bool isDark)
	{
		if (string.IsNullOrWhiteSpace(text)) return new List<HighlightSpan>();

		return await Task.Run(() =>
		{
			var highlights = new List<HighlightSpan>();
			try
			{
				var tree = CSharpSyntaxTree.ParseText(text);
				var root = tree.GetRoot();

				Color GetColor(string scope, Color fallback)
				{
					if (theme.Contains(scope)) return GetColorFromHex(theme[scope].Foreground);
					return fallback;
				}

				// Definição de Cores
				var colorComment = GetColor(ColorCode.Common.ScopeName.Comment, Colors.Green);
				var colorKeyword = GetColor(ColorCode.Common.ScopeName.Keyword, isDark ? Colors.DeepSkyBlue : Colors.Blue);
				var colorControl = GetColor(ThemeHelper.ControlKeywordScope, isDark ? Colors.Violet : Colors.Purple);
				var colorString = GetColor(ColorCode.Common.ScopeName.String, isDark ? Colors.Orange : Colors.Brown);
				var colorNumber = GetColor(ColorCode.Common.ScopeName.Number, isDark ? Colors.LightGreen : Colors.DarkGreen);
				var colorClass = GetColor(ThemeHelper.ClassScope, isDark ? Colors.Teal : Colors.DarkCyan);
				var colorInterface = GetColor(ThemeHelper.InterfaceScope, isDark ? Colors.LightGreen : Colors.DarkGreen);
				var colorMethod = GetColor(ThemeHelper.MethodScope, isDark ? Colors.LightYellow : Colors.DarkGoldenrod);
				var colorParam = GetColor(ThemeHelper.ParameterScope, isDark ? Colors.LightSkyBlue : Colors.DarkBlue);
				var colorPunctuation = GetColor(ThemeHelper.PunctuationScope, isDark ? Colors.Gold : Colors.Black);

				// A. Trivia (Comentários)
				foreach (var trivia in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia)))
				{
					highlights.Add(new HighlightSpan { Start = trivia.Span.Start, Length = trivia.Span.Length, Color = colorComment });
				}

				// B. Nodes (Semântica)
				foreach (var node in root.DescendantNodes())
				{
					// Identificadores (Classes, Tipos)
					if (node is IdentifierNameSyntax idNode && IsTypeUsage(idNode))
					{
						var color = IsInterface(idNode.Identifier.Text) ? colorInterface : colorClass;
						highlights.Add(new HighlightSpan { Start = idNode.Span.Start, Length = idNode.Span.Length, Color = color });
					}
					// Genéricos (List<T>)
					else if (node is GenericNameSyntax genericNode)
					{
						var color = IsInterface(genericNode.Identifier.Text) ? colorInterface : colorClass;
						highlights.Add(new HighlightSpan { Start = genericNode.Identifier.Span.Start, Length = genericNode.Identifier.Span.Length, Color = color });
					}
					// Métodos
					else if (node is MethodDeclarationSyntax methodDecl)
					{
						highlights.Add(new HighlightSpan { Start = methodDecl.Identifier.Span.Start, Length = methodDecl.Identifier.Span.Length, Color = colorMethod });
						// Tipo de retorno
						if (methodDecl.ReturnType is IdentifierNameSyntax retId)
							highlights.Add(new HighlightSpan { Start = retId.Span.Start, Length = retId.Span.Length, Color = colorClass });
					}
					// Chamadas de Método (obj.Metodo())
					else if (node is InvocationExpressionSyntax invocation)
					{
						if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
							highlights.Add(new HighlightSpan { Start = memberAccess.Name.Span.Start, Length = memberAccess.Name.Span.Length, Color = colorMethod });
						else if (invocation.Expression is IdentifierNameSyntax directCall)
							highlights.Add(new HighlightSpan { Start = directCall.Span.Start, Length = directCall.Span.Length, Color = colorMethod });
					}
					// Parâmetros
					else if (node is ParameterSyntax param)
					{
						highlights.Add(new HighlightSpan { Start = param.Identifier.Span.Start, Length = param.Identifier.Span.Length, Color = colorParam });
					}
				}

				// C. Tokens (Keywords, Pontuação)
				foreach (var token in root.DescendantTokens())
				{
					var kind = token.Kind();

					if (IsPunctuation(kind))
						highlights.Add(new HighlightSpan { Start = token.Span.Start, Length = token.Span.Length, Color = colorPunctuation });
					else if (IsControlKeyword(kind))
						highlights.Add(new HighlightSpan { Start = token.Span.Start, Length = token.Span.Length, Color = colorControl });
					else if (kind == SyntaxKind.StringLiteralToken || kind == SyntaxKind.InterpolatedStringTextToken)
						highlights.Add(new HighlightSpan { Start = token.Span.Start, Length = token.Span.Length, Color = colorString });
					else if (kind == SyntaxKind.NumericLiteralToken)
						highlights.Add(new HighlightSpan { Start = token.Span.Start, Length = token.Span.Length, Color = colorNumber });
					else if (IsGenericKeyword(kind))
						highlights.Add(new HighlightSpan { Start = token.Span.Start, Length = token.Span.Length, Color = colorKeyword });
				}
			}
			catch { }

			return highlights;
		});
	}

	// PASSO 2: Aplicação na UI (Executar via DispatcherQueue)
	public void ApplyHighlights(RichEditBox editor, List<HighlightSpan> highlights)
	{
		if (editor == null || highlights == null) return;

		editor.Document.GetText(TextGetOptions.None, out string currentText);
		int currentLength = currentText.Length;

		// SEGURANÇA: Se o texto mudou (ex: deletou) e o highlight aponta pra fora, aborta.
		if (highlights.Any(h => h.Start + h.Length > currentLength + 1)) return;

		editor.Document.BatchDisplayUpdates();

		try
		{
			var theme = ThemeHelper.GetCurrentThemeStyle();
			bool isDark = ThemeHelper.IsDarkTheme();

			var defaultColor = theme.Contains(ColorCode.Common.ScopeName.PlainText)
				? GetColorFromHex(theme[ColorCode.Common.ScopeName.PlainText].Foreground)
				: (isDark ? Colors.White : Colors.Black);

			// Reseta cor base
			editor.Document.GetRange(0, currentLength + 1).CharacterFormat.ForegroundColor = defaultColor;

			// Aplica highlights
			foreach (var span in highlights)
			{
				int start = Math.Clamp(span.Start, 0, currentLength);
				int end = Math.Clamp(span.Start + span.Length, 0, currentLength);

				if (end > start)
				{
					editor.Document.GetRange(start, end).CharacterFormat.ForegroundColor = span.Color;
				}
			}
		}
		catch { }
		finally
		{
			// IMPORTANTE: Não restauramos seleção nem scroll aqui para evitar "pulos".
			editor.Document.ApplyDisplayUpdates();
		}
	}

	private bool IsTypeUsage(IdentifierNameSyntax node)
	{
		var parent = node.Parent;
		return parent is VariableDeclarationSyntax || parent is ObjectCreationExpressionSyntax ||
			   parent is ParameterSyntax || parent is MethodDeclarationSyntax ||
			   parent is GenericNameSyntax || parent is TypeArgumentListSyntax ||
			   parent is CastExpressionSyntax || parent is SimpleBaseTypeSyntax;
	}

	private bool IsInterface(string text) => text.Length > 2 && _interfaceRegex.IsMatch(text);

	private bool IsPunctuation(SyntaxKind kind) =>
		kind == SyntaxKind.OpenParenToken || kind == SyntaxKind.CloseParenToken ||
		kind == SyntaxKind.OpenBraceToken || kind == SyntaxKind.CloseBraceToken ||
		kind == SyntaxKind.OpenBracketToken || kind == SyntaxKind.CloseBracketToken ||
		kind == SyntaxKind.SemicolonToken || kind == SyntaxKind.CommaToken || kind == SyntaxKind.DotToken;

	private bool IsControlKeyword(SyntaxKind kind) =>
		kind == SyntaxKind.ReturnKeyword || kind == SyntaxKind.IfKeyword || kind == SyntaxKind.ElseKeyword ||
		kind == SyntaxKind.TryKeyword || kind == SyntaxKind.CatchKeyword || kind == SyntaxKind.FinallyKeyword ||
		kind == SyntaxKind.ForKeyword || kind == SyntaxKind.ForEachKeyword || kind == SyntaxKind.WhileKeyword ||
		kind == SyntaxKind.SwitchKeyword || kind == SyntaxKind.AwaitKeyword || kind == SyntaxKind.ThrowKeyword;

	private bool IsGenericKeyword(SyntaxKind kind) => kind.ToString().EndsWith("Keyword") && !IsControlKeyword(kind);

	private Color GetColorFromHex(string hex)
	{
		if (string.IsNullOrEmpty(hex)) return Colors.Black;
		hex = hex.Replace("#", "");
		byte a = 255, r = 0, g = 0, b = 0;
		if (hex.Length == 6) { r = Convert.ToByte(hex.Substring(0, 2), 16); g = Convert.ToByte(hex.Substring(2, 2), 16); b = Convert.ToByte(hex.Substring(4, 2), 16); }
		else if (hex.Length == 8) { a = Convert.ToByte(hex.Substring(0, 2), 16); r = Convert.ToByte(hex.Substring(2, 2), 16); g = Convert.ToByte(hex.Substring(4, 2), 16); b = Convert.ToByte(hex.Substring(6, 2), 16); }
		return Color.FromArgb(a, r, g, b);
	}
}