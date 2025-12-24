using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Services;

public class RobustMergerRewriter : CSharpSyntaxRewriter
{
	private readonly string _targetClassName;
	private readonly List<MemberDeclarationSyntax> _snippetMembers;

	public RobustMergerRewriter(string targetClassName, SyntaxList<MemberDeclarationSyntax> snippetMembers)
	{
		_targetClassName = targetClassName;
		_snippetMembers = snippetMembers.ToList();
	}

	public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		// Só altera a classe alvo
		if (node.Identifier.Text != _targetClassName) return base.VisitClassDeclaration(node);

		var updatedNode = node;
		var membersToAdd = new List<MemberDeclarationSyntax>();

		foreach (var newMember in _snippetMembers)
		{
			bool isReplaced = false;
			var existingMember = FindMatchingMember(updatedNode, newMember);

			if (existingMember != null)
			{
				// Substitui o membro existente pelo novo (atualização/correção)
				updatedNode = updatedNode.ReplaceNode(existingMember, newMember);
				isReplaced = true;
			}
			else
			{
				// Prepara para adicionar se não existir
				membersToAdd.Add(newMember);
			}
		}

		// Adiciona novos métodos/propriedades que não existiam
		if (membersToAdd.Any())
		{
			updatedNode = updatedNode.AddMembers(membersToAdd.ToArray());
		}

		return updatedNode;
	}

	private MemberDeclarationSyntax? FindMatchingMember(ClassDeclarationSyntax classNode, MemberDeclarationSyntax memberToFind)
	{
		if (memberToFind is MethodDeclarationSyntax method)
		{
			// Busca por assinatura de método (Nome)
			// Nota: Para ser perfeito, deveria checar parâmetros, mas por nome já resolve 90% dos casos de snippet
			return classNode.Members.OfType<MethodDeclarationSyntax>()
							.FirstOrDefault(m => m.Identifier.Text == method.Identifier.Text);
		}

		if (memberToFind is ConstructorDeclarationSyntax ctor)
		{
			return classNode.Members.OfType<ConstructorDeclarationSyntax>()
							.FirstOrDefault(c => c.Identifier.Text == ctor.Identifier.Text);
		}

		if (memberToFind is PropertyDeclarationSyntax prop)
		{
			return classNode.Members.OfType<PropertyDeclarationSyntax>()
							.FirstOrDefault(p => p.Identifier.Text == prop.Identifier.Text);
		}

		if (memberToFind is FieldDeclarationSyntax field)
		{
			// Lógica simples para campos: verifica o nome da primeira variável declarada
			var fieldName = field.Declaration.Variables.FirstOrDefault()?.Identifier.Text;
			if (fieldName != null)
			{
				return classNode.Members.OfType<FieldDeclarationSyntax>()
					.FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));
			}
		}

		return null;
	}
}