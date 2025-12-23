//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using System.Collections.Generic;
//using System.Linq;

//namespace ContextWinUI.Core.Services; // Ajuste o namespace conforme seu projeto

//public class MethodFilterRewriter : CSharpSyntaxRewriter
//{
//	private readonly HashSet<string>? _allowedSignatures;
//	private readonly bool _removeUsings;
//	private readonly bool _removeComments;

//	// Construtor: Se allowedSignatures for null, ele não filtra métodos (copia tudo)
//	public MethodFilterRewriter(IEnumerable<string>? allowedSignatures, bool removeUsings, bool removeComments)
//	{
//		_allowedSignatures = allowedSignatures != null ? new HashSet<string>(allowedSignatures.Select(s => s.Replace(" ", ""))) : null;

//		_removeUsings = removeUsings;
//		_removeComments = removeComments;
//	}

//	// =========================================================================
//	// LÓGICA PRINCIPAL: Filtra APENAS métodos. 
//	// Campos, Propriedades e Construtores são mantidos pelo comportamento padrão.
//	// =========================================================================
//	public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
//	{
//		if (_allowedSignatures == null)
//		{
//			return base.VisitMethodDeclaration(node);
//		}

//		var paramsList = node.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "var");
//		var signature = $"{node.Identifier.Text}({string.Join(",", paramsList)})"; // Note a vírgula sem espaço aqui propositalmente para normalização

//		// Normaliza a assinatura gerada removendo espaços
//		var normalizedSignature = signature.Replace(" ", "");

//		if (_allowedSignatures.Contains(normalizedSignature))
//		{
//			return base.VisitMethodDeclaration(node);
//		}

//		// Se não estiver na lista de permitidos, remove o método (retorna null)
//		return null;
//	}

//	// =========================================================================
//	// LÓGICA SECUNDÁRIA: Limpeza de Usings e Comentários
//	// =========================================================================

//	public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
//	{
//		if (_removeUsings)
//		{
//			return null; // Remove o using
//		}
//		return base.VisitUsingDirective(node);
//	}

//	public override SyntaxToken VisitToken(SyntaxToken token)
//	{
//		// Remove comentários preservando a estrutura do código
//		if (_removeComments)
//		{
//			// Filtra LeadingTrivia (comentários antes do token)
//			var newLeading = token.LeadingTrivia.Where(t => !IsComment(t));
//			// Filtra TrailingTrivia (comentários na mesma linha após o token)
//			var newTrailing = token.TrailingTrivia.Where(t => !IsComment(t));

//			return token.WithLeadingTrivia(newLeading).WithTrailingTrivia(newTrailing);
//		}
//		return base.VisitToken(token);
//	}

//	private bool IsComment(SyntaxTrivia trivia)
//	{
//		return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
//			   trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
//			   trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
//			   trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) ||
//			   trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);
//	}
//}