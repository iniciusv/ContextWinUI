using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Core.Services; // Ajuste o namespace conforme seu projeto

public class MethodFilterRewriter : CSharpSyntaxRewriter
{
	private readonly HashSet<string>? _allowedSignatures;
	private readonly bool _removeUsings;
	private readonly bool _removeComments;

	// Construtor: Se allowedSignatures for null, ele não filtra métodos (copia tudo)
	public MethodFilterRewriter(IEnumerable<string>? allowedSignatures, bool removeUsings, bool removeComments)
	{
		_allowedSignatures = allowedSignatures != null ? new HashSet<string>(allowedSignatures) : null;
		_removeUsings = removeUsings;
		_removeComments = removeComments;
	}

	// =========================================================================
	// LÓGICA PRINCIPAL: Filtra APENAS métodos. 
	// Campos, Propriedades e Construtores são mantidos pelo comportamento padrão.
	// =========================================================================
	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
	{
		// Se não há lista de permitidos, mantém o método (retorna a visita padrão)
		if (_allowedSignatures == null)
		{
			return base.VisitMethodDeclaration(node);
		}

		// Reconstrói a assinatura do método para comparar com o que temos salvo no ViewModel.
		// Deve bater exatamente com a lógica usada no RoslynCsharpStrategy.
		var paramsList = node.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
		var signature = $"{node.Identifier.Text}({string.Join(", ", paramsList)})";

		// VERIFICAÇÃO:
		// Se a assinatura está na lista de permitidos, mantemos o método (base.Visit).
		// Se não está, retornamos null (o que remove o nó da árvore de sintaxe).
		if (_allowedSignatures.Contains(signature))
		{
			return base.VisitMethodDeclaration(node);
		}

		// Retornar null remove este método do código final
		return null;
	}

	// =========================================================================
	// LÓGICA SECUNDÁRIA: Limpeza de Usings e Comentários
	// =========================================================================

	public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
	{
		if (_removeUsings)
		{
			return null; // Remove o using
		}
		return base.VisitUsingDirective(node);
	}

	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		// Remove comentários preservando a estrutura do código
		if (_removeComments)
		{
			// Filtra LeadingTrivia (comentários antes do token)
			var newLeading = token.LeadingTrivia.Where(t => !IsComment(t));
			// Filtra TrailingTrivia (comentários na mesma linha após o token)
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