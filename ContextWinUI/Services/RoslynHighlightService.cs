using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using ColorCode.Styling; // Para acessar o StyleDictionary
using Windows.UI;

namespace ContextWinUI.Services;

public class RoslynHighlightService
{
	public async Task HighlightAsync(RichTextBlock richTextBlock, string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			richTextBlock.Blocks.Clear();
			return;
		}

		// 1. Parse do código (Rápido)
		var tree = CSharpSyntaxTree.ParseText(code);
		var root = await tree.GetRootAsync();

		// 2. Compilação (Necessária para entender o significado dos símbolos)
		// Nota: Em um app real, idealmente você cacheia essa compilação se o código não mudou
		var compilation = CSharpCompilation.Create("Highlighting")
			.AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
			.AddSyntaxTrees(tree);

		var semanticModel = compilation.GetSemanticModel(tree);
		var styles = ThemeHelper.GetCurrentThemeStyle();

		// Limpa o controle UI (tem que ser na Thread UI)
		richTextBlock.Blocks.Clear();
		var paragraph = new Paragraph();

		// 3. Iterar sobre todos os tokens (incluindo espaços e comentários)
		// DescendantTokens() pega todas as palavras, simbolos, etc.
		foreach (var token in root.DescendantTokens())
		{
			// A. Leading Trivia (Espaços e Comentários ANTES do token)
			foreach (var trivia in token.LeadingTrivia)
			{
				AddTrivia(paragraph, trivia, styles);
			}

			// B. O Token em si (A palavra chave, o nome da classe, etc)
			AddToken(paragraph, token, semanticModel, styles);

			// C. Trailing Trivia (Espaços e Comentários DEPOIS do token)
			foreach (var trivia in token.TrailingTrivia)
			{
				AddTrivia(paragraph, trivia, styles);
			}
		}

		richTextBlock.Blocks.Add(paragraph);
	}

	private void AddTrivia(Paragraph paragraph, SyntaxTrivia trivia, StyleDictionary styles)
	{
		string color = null;

		if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
			trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
			trivia.IsKind(SyntaxKind.XmlComment))
		{
			color = GetColor(styles, "Comment"); // Usa a chave do ColorCode
		}

		paragraph.Inlines.Add(CreateRun(trivia.ToString(), color ?? GetColor(styles, "Plain Text")));
	}

	private void AddToken(Paragraph paragraph, SyntaxToken token, SemanticModel model, StyleDictionary styles)
	{
		string colorKey = "Plain Text";

		// 1. Verificações Simples (Keywords, Strings, Números) - Não precisa de SemanticModel
		if (token.IsKeyword())
		{
			colorKey = "Keyword";
		}
		else if (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.CharacterLiteralToken))
		{
			colorKey = "String";
		}
		else if (token.IsKind(SyntaxKind.NumericLiteralToken))
		{
			colorKey = "Number";
		}
		// 2. Identificadores (Nomes de classes, métodos, variáveis) - Precisa de SemanticModel
		else if (token.IsKind(SyntaxKind.IdentifierToken))
		{
			var node = token.Parent;
			ISymbol symbol = null;

			// Tenta obter o símbolo
			if (node != null)
			{
				symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
			}

			if (symbol != null)
			{
				colorKey = symbol.Kind switch
				{
					SymbolKind.NamedType => (symbol as INamedTypeSymbol)?.TypeKind == TypeKind.Interface ? ThemeHelper.InterfaceScope :
											(symbol as INamedTypeSymbol)?.TypeKind == TypeKind.Struct ? ThemeHelper.StructScope :
											(symbol as INamedTypeSymbol)?.TypeKind == TypeKind.Enum ? ThemeHelper.EnumScope :
											ThemeHelper.ClassScope,
					SymbolKind.Method => ThemeHelper.MethodScope,
					SymbolKind.Property => ThemeHelper.PropertyScope,
					SymbolKind.Field => ThemeHelper.FieldScope,
					SymbolKind.Parameter => ThemeHelper.ParameterScope,
					SymbolKind.Local => ThemeHelper.LocalVariableScope,
					SymbolKind.Namespace => ThemeHelper.NamespaceScope,
					_ => ThemeHelper.ClassScope // Fallback
				};
			}
			else
			{
				// Se o Roslyn não conseguiu resolver (ex: falta de referências), 
				// usamos heuristicas simples baseadas na sintaxe
				if (node is MethodDeclarationSyntax) colorKey = ThemeHelper.MethodScope;
				else if (node is ClassDeclarationSyntax) colorKey = ThemeHelper.ClassScope;
				else if (node is InterfaceDeclarationSyntax) colorKey = ThemeHelper.InterfaceScope;
			}
		}

		paragraph.Inlines.Add(CreateRun(token.Text, GetColor(styles, colorKey)));
	}

	private Run CreateRun(string text, string hexColor)
	{
		var run = new Run { Text = text };
		if (!string.IsNullOrEmpty(hexColor))
		{
			run.Foreground = new SolidColorBrush(GetColorFromHex(hexColor));
		}
		return run;
	}

	private string GetColor(StyleDictionary styles, string scope)
	{
		if (styles.Contains(scope))
		{
			return styles[scope].Foreground;
		}
		return styles["Plain Text"].Foreground;
	}

	private Color GetColorFromHex(string hex)
	{
		hex = hex.Replace("#", "");
		byte a = 255;
		byte r = 255;
		byte g = 255;
		byte b = 255;

		if (hex.Length == 6)
		{
			r = Convert.ToByte(hex.Substring(0, 2), 16);
			g = Convert.ToByte(hex.Substring(2, 2), 16);
			b = Convert.ToByte(hex.Substring(4, 2), 16);
		}
		else if (hex.Length == 8)
		{
			a = Convert.ToByte(hex.Substring(0, 2), 16);
			r = Convert.ToByte(hex.Substring(2, 2), 16);
			g = Convert.ToByte(hex.Substring(4, 2), 16);
			b = Convert.ToByte(hex.Substring(6, 2), 16);
		}

		return Color.FromArgb(a, r, g, b);
	}
}