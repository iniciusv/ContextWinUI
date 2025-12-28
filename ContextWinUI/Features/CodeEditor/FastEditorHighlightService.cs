using ContextWinUI.Features.CodeEditor;
using ContextWinUI.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.UI;
using System.Collections.Generic;
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
				var tree = CSharpSyntaxTree.ParseText(text);
				var root = tree.GetRoot();

				// Paleta de cores expandida
				var colors = isDark ? GetDarkPalette() : GetLightPalette();

				// 1. Trivia (Comentários)
				foreach (var trivia in root.DescendantTrivia())
				{
					if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
						trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
					{
						highlights.Add(new HighlightSpan(trivia.SpanStart, trivia.Span.Length, colors.Comment));
					}
				}

				// 2. Tokens e Estruturas
				foreach (var token in root.DescendantTokens())
				{
					var kind = token.Kind();

					// --- LITERAIS E KEYWORDS ---
					if (kind == SyntaxKind.StringLiteralToken || kind == SyntaxKind.InterpolatedStringTextToken)
					{
						highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.String));
					}
					else if (kind == SyntaxKind.NumericLiteralToken)
					{
						highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Number));
					}
					else if (token.IsKeyword())
					{
						// Diferencia Control Keywords (return, if, await) se quiser
						if (IsControlKeyword(kind))
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.ControlKeyword));
						else
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Keyword));
					}

					// --- IDENTIFICADORES (A parte Inteligente) ---
					else if (kind == SyntaxKind.IdentifierToken)
					{
						var parent = token.Parent;

						// Se o pai for null, ignora
						if (parent == null) continue;

						// A. ATRIBUTOS: [RelayCommand], [ObservableProperty]
						// Estrutura: AttributeSyntax -> IdentifierNameSyntax -> Token
						if (parent.Parent is AttributeSyntax ||
						   (parent is IdentifierNameSyntax idName && idName.Parent is AttributeSyntax))
						{
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Class)); // Atributos são classes
						}

						// B. DECLARAÇÃO DE CLASSES / MÉTODOS
						else if (parent is ClassDeclarationSyntax || parent is InterfaceDeclarationSyntax || parent is RecordDeclarationSyntax)
						{
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Class));
						}
						else if (parent is MethodDeclarationSyntax)
						{
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Method));
						}

						// C. TIPOS E CLASSES (Ex: 'Task', 'List', 'var')
						// Detecta retorno de métodos: Task Metodo()
						// Detecta instanciação: new Task()
						// Detecta genéricos: List<Task>
						else if (IsTypeContext(parent))
						{
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Class));
						}

						// D. VARIÁVEIS E PARÂMETROS (Declaração)
						else if (parent is ParameterSyntax)
						{
							// Ex: (string texto) -> 'texto'
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Parameter));
						}
						else if (parent is VariableDeclaratorSyntax)
						{
							// Ex: int contador = 0 -> 'contador'
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Variable));
						}

						// E. CHAMADAS DE MÉTODO
						// Ex: Console.WriteLine() -> 'WriteLine'
						else if (parent is SimpleNameSyntax && parent.Parent is InvocationExpressionSyntax)
						{
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Method));
						}
						else if (parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == parent && memberAccess.Parent is InvocationExpressionSyntax)
						{
							// Ex: objeto.Metodo() -> 'Metodo'
							highlights.Add(new HighlightSpan(token.SpanStart, token.Span.Length, colors.Method));
						}
					}
				}

				return highlights;
			});
		}

		// Ajuda a identificar se o identificador está sendo usado como um Tipo (Classe, Struct, Enum)
		private bool IsTypeContext(SyntaxNode parent)
		{
			// Task Metodo() -> 'Task' é o ReturnType
			if (parent.Parent is MethodDeclarationSyntax methodDecl && methodDecl.ReturnType == parent) return true;

			// new Task() -> 'Task' é o Type do ObjectCreation
			if (parent.Parent is ObjectCreationExpressionSyntax) return true;

			// List<Task> -> 'Task' está dentro de TypeArgumentList
			if (parent.Parent is TypeArgumentListSyntax) return true;

			// private Task _campo -> 'Task' é o tipo do campo
			if (parent.Parent is FieldDeclarationSyntax) return true;

			// private Task Propriedade { get; } -> 'Task' é o tipo da propriedade
			if (parent.Parent is PropertyDeclarationSyntax prop && prop.Type == parent) return true;

			// Variável local: Task t = ...
			if (parent.Parent is VariableDeclarationSyntax) return true;

			// Herança: class A : B -> 'B' é BaseType
			if (parent.Parent is SimpleBaseTypeSyntax) return true;

			return false;
		}

		private bool IsControlKeyword(SyntaxKind kind)
		{
			return kind == SyntaxKind.IfKeyword || kind == SyntaxKind.ElseKeyword ||
				   kind == SyntaxKind.ReturnKeyword || kind == SyntaxKind.AwaitKeyword ||
				   kind == SyntaxKind.ForKeyword || kind == SyntaxKind.ForEachKeyword ||
				   kind == SyntaxKind.WhileKeyword || kind == SyntaxKind.TryKeyword ||
				   kind == SyntaxKind.CatchKeyword || kind == SyntaxKind.FinallyKeyword;
		}

		// Definição da Paleta de Cores (Estilo VS Code Dark+)
		private EditorPalette GetDarkPalette()
		{
			return new EditorPalette
			{
				Comment = Color.FromArgb(255, 106, 153, 85),        // #6A9955 (Verde)
				String = Color.FromArgb(255, 206, 145, 120),       // #CE9178 (Laranja/Salmão)
				Keyword = Color.FromArgb(255, 86, 156, 214),       // #569CD6 (Azul)
				ControlKeyword = Color.FromArgb(255, 197, 134, 192), // #C586C0 (Roxo - return, await)
				Number = Color.FromArgb(255, 181, 206, 168),       // #B5CEA8 (Verde Claro)
				Class = Color.FromArgb(255, 78, 201, 176),         // #4EC9B0 (Verde Água - Task, RelayCommand)
				Method = Color.FromArgb(255, 220, 220, 170),       // #DCDCAA (Amarelo - Métodos)
				Parameter = Color.FromArgb(255, 156, 220, 254),    // #9CDCFE (Azul Claro - Parâmetros)
				Variable = Color.FromArgb(255, 156, 220, 254)      // #9CDCFE (Azul Claro - Variáveis)
			};
		}

		private EditorPalette GetLightPalette()
		{
			return new EditorPalette
			{
				Comment = Color.FromArgb(255, 0, 128, 0),
				String = Color.FromArgb(255, 163, 21, 21),
				Keyword = Color.FromArgb(255, 0, 0, 255),
				ControlKeyword = Color.FromArgb(255, 143, 8, 196),
				Number = Color.FromArgb(255, 9, 136, 90),
				Class = Color.FromArgb(255, 43, 145, 175),
				Method = Color.FromArgb(255, 116, 83, 31),
				Parameter = Color.FromArgb(255, 31, 55, 127),
				Variable = Color.FromArgb(255, 31, 55, 127)
			};
		}

		private struct EditorPalette
		{
			public Color Comment;
			public Color String;
			public Color Keyword;
			public Color ControlKeyword;
			public Color Number;
			public Color Class;
			public Color Method;
			public Color Parameter;
			public Color Variable;
		}

		// Construtor auxiliar para o HighlightSpan se não existir
		// Se você já tem a struct HighlightSpan definida, pode ignorar o construtor customizado e usar o inicializador de objeto
	}

	// Extensão para construtor limpo (opcional, coloque no HighlightSpan.cs se preferir)
	public static class HighlightExtensions
	{
		public static HighlightSpan Create(int start, int length, Color color) => new HighlightSpan { Start = start, Length = length, Color = color };
	}
}