using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Services;

// O Rewriter é quem faz a "cirurgia" na árvore original
public class SnippetMergerRewriter : CSharpSyntaxRewriter
{
	private readonly ClassDeclarationSyntax _snippetClass;

	public SnippetMergerRewriter(ClassDeclarationSyntax snippetClass)
	{
		_snippetClass = snippetClass;
	}

	// Ao visitar uma classe no arquivo original...
	public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
	{
		// Se for a mesma classe do snippet
		if (node.Identifier.Text == _snippetClass.Identifier.Text)
		{
			// Vamos procurar métodos/construtores dentro do snippet para aplicar
			var updatedNode = node;

			// 1. Processar Construtores
			var snippetCtors = _snippetClass.Members.OfType<ConstructorDeclarationSyntax>();
			foreach (var snippetCtor in snippetCtors)
			{
				// Busca o construtor correspondente no original (pela lista de parâmetros seria o ideal, aqui simplificado pelo nome da classe)
				var originalCtor = updatedNode.Members.OfType<ConstructorDeclarationSyntax>()
											  .FirstOrDefault(c => c.Identifier.Text == snippetCtor.Identifier.Text);

				if (originalCtor != null)
				{
					// Faz o merge do corpo do construtor
					var newCtor = MergeMethodBody(originalCtor, snippetCtor);
					updatedNode = updatedNode.ReplaceNode(originalCtor, newCtor);
				}
			}

			// Aqui você expandiria para Methods, Properties, etc.
			return updatedNode;
		}

		return base.VisitClassDeclaration(node);
	}

	private ConstructorDeclarationSyntax MergeMethodBody(ConstructorDeclarationSyntax original, ConstructorDeclarationSyntax snippet)
	{
		if (snippet.Body == null || original.Body == null) return original;

		var newStatements = new List<StatementSyntax>();
		var originalStatements = original.Body.Statements.ToList();

		// Estratégia de Merge:
		// Iteramos sobre o Snippet.
		// - Se for "// ...", copiamos o que estava no original até o próximo ponto de ancoragem.
		// - Se for código, inserimos/substituímos.

		// SIMPLIFICAÇÃO INTELIGENTE:
		// Como implementar um diff completo é complexo, vamos focar no caso de uso do usuário:
		// Detectar variáveis chaves (AiChanges, semanticIndexService) e atualizar/inserir.

		var snippetStatements = snippet.Body.Statements;

		// Copia tudo do original primeiro
		newStatements.AddRange(originalStatements);

		foreach (var statement in snippetStatements)
		{
			// Ignora placeholders (comentários não viram StatementSyntax facilmente, mas blocos vazios sim)
			if (statement.ToString().Contains("...")) continue;

			// Tenta identificar se é uma atribuição de variável
			if (statement is ExpressionStatementSyntax expr && expr.Expression is AssignmentExpressionSyntax assignment)
			{
				var variableName = (assignment.Left as IdentifierNameSyntax)?.Identifier.Text;

				if (variableName != null)
				{
					// Procura se essa variável já é atribuída no original
					var indexInOriginal = newStatements.FindIndex(s =>
						s is ExpressionStatementSyntax origExpr &&
						origExpr.Expression is AssignmentExpressionSyntax origAssign &&
						(origAssign.Left as IdentifierNameSyntax)?.Identifier.Text == variableName);

					if (indexInOriginal >= 0)
					{
						// SUBSTITUIÇÃO: Encontrou a mesma variável sendo atribuída, troca a linha
						newStatements[indexInOriginal] = statement.WithTriviaFrom(newStatements[indexInOriginal]);
					}
					else
					{
						// INSERÇÃO: É uma variável nova (ex: semanticIndexService)
						// Lógica: Inserir antes da próxima instrução conhecida do snippet?
						// Por simplicidade: Insere antes do final ou baseado em âncoras vizinhas.

						// Heurística: Inserir antes da última atribuição encontrada
						int insertPos = newStatements.Count > 0 ? newStatements.Count - 1 : 0;
						newStatements.Insert(insertPos, statement);
					}
				}
			}
			// Trata declarações locais (var x = new ...)
			else if (statement is LocalDeclarationStatementSyntax localDecl)
			{
				var varName = localDecl.Declaration.Variables.First().Identifier.Text;
				var exists = newStatements.Any(s => s is LocalDeclarationStatementSyntax l && l.Declaration.Variables.First().Identifier.Text == varName);

				if (!exists)
				{
					// Insere novas variáveis. 
					// Melhoria: Tentar achar a linha "vizinha" no snippet para saber onde inserir no original.
					// Para o exemplo do usuário, inserir antes de 'AiChanges' seria o ideal.

					// Encontra o 'AiChanges' no original para usar de âncora
					var anchorIndex = newStatements.FindIndex(s => s.ToString().Contains("AiChanges"));
					if (anchorIndex >= 0)
						newStatements.Insert(anchorIndex, statement);
					else
						newStatements.Add(statement); // Fallback
				}
			}
		}

		return original.WithBody(SyntaxFactory.Block(newStatements));
	}
}