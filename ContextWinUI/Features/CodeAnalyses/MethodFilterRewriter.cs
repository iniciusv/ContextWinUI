using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class MethodFilterRewriter : CSharpSyntaxRewriter
{
	private readonly HashSet<string>? _allowedSignatures; // Agora pode ser nulo
	private readonly bool _removeUsings;
	private readonly bool _removeComments;

	// Construtor modificado
	public MethodFilterRewriter(IEnumerable<string>? allowedSignatures, bool removeUsings, bool removeComments)
	{
		_allowedSignatures = allowedSignatures != null ? new HashSet<string>(allowedSignatures) : null;
		_removeUsings = removeUsings;
		_removeComments = removeComments;
	}

	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		// SE _allowedSignatures FOR NULL, MANTÉM TUDO (Modo Arquivo Inteiro)
		if (_allowedSignatures == null)
		{
			return base.VisitMethodDeclaration(node);
		}

		// Lógica de filtro específica (Modo Seleção Parcial)
		var paramsList = node.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
		var signature = $"{node.Identifier.Text}({string.Join(", ", paramsList)})";

		if (_allowedSignatures.Contains(signature))
		{
			return base.VisitMethodDeclaration(node);
		}
		return null;
	}


	// 2. Remoção de Usings
	public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
	{
		if (_removeUsings)
		{
			return null; // Remove o using completamente
		}
		return base.VisitUsingDirective(node);
	}

	// 3. Remoção de Comentários (Manipulação de Trivia nos Tokens)
	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		if (_removeComments)
		{
			var newLeading = token.LeadingTrivia.Where(t => !IsComment(t));
			var newTrailing = token.TrailingTrivia.Where(t => !IsComment(t));
			return token.WithLeadingTrivia(newLeading).WithTrailingTrivia(newTrailing);
		}
		return base.VisitToken(token);
	}

	private bool IsComment(SyntaxTrivia trivia)
	{
		return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
			   trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
			   trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
			   trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||
			   trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);
	}
}