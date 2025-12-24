using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeEditor;

public class CodeTransformationService
{
	public class TransformationOptions
	{
		public bool HideComments { get; set; }
		public bool CollapseMethods { get; set; }
		public int MaxLinesForCollapse { get; set; } = 10;
	}

	public string TransformCode(string sourceCode, TransformationOptions options)
	{
		if (string.IsNullOrWhiteSpace(sourceCode)) return "";
		if (!options.HideComments && !options.CollapseMethods) return sourceCode;

		try
		{
			var tree = CSharpSyntaxTree.ParseText(sourceCode);
			var root = tree.GetRoot();

			var rewriter = new ViewModeRewriter(options);
			var newRoot = rewriter.Visit(root);

			return newRoot.ToFullString();
		}
		catch
		{
			// Em caso de erro de parse (código inválido), retorna o original
			return sourceCode;
		}
	}

	private class ViewModeRewriter : CSharpSyntaxRewriter
	{
		private readonly TransformationOptions _options;

		public ViewModeRewriter(TransformationOptions options)
		{
			_options = options;
		}

		// 1. Lógica para Ocultar Comentários
		public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
		{
			if (_options.HideComments)
			{
				// Verifica todos os tipos de comentários comuns e documentação XML
				if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||           // // Comentário
					trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||            // /* Bloco */
					trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || // /// Resumo
					trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||  // /** Bloco Doc */
					trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia))     // Símbolos '///' isolados
				{
					// Retorna trivia vazia (remove o comentário)
					return default;
				}
			}
			return base.VisitTrivia(trivia);
		}

		// 2. Lógica para Ocultar XML Docs (/// Summary)
		public override SyntaxNode? VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
		{
			if (_options.HideComments) return null; // Remove o bloco de documentação inteiro
			return base.VisitDocumentationCommentTrivia(node);
		}

		// 3. Lógica para Colapsar Métodos
		public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			if (_options.CollapseMethods && node.Body != null)
			{
				// Conta linhas aproximadas baseadas no Span do corpo
				var lineSpan = node.Body.SyntaxTree.GetLineSpan(node.Body.Span);
				int lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line;

				if (lineCount > _options.MaxLinesForCollapse)
				{
					// Cria um novo corpo com apenas "{ ... }"
					// Preserva a formatação básica (espaços antes)
					var leadingTrivia = node.Body.GetLeadingTrivia();
					var collapsedBlock = SyntaxFactory.Block(
						SyntaxFactory.ParseStatement(" /* ... código oculto ... */ ")
					).WithLeadingTrivia(leadingTrivia);

					return node.WithBody(collapsedBlock);
				}
			}
			return base.VisitMethodDeclaration(node);
		}
	}
}