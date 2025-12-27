using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Features.GraphView;

public class SyntaxTreeDiffer : IRoslynTreeDiffer
{
	public SyntaxTreeComparison Compare(SyntaxNode originalRoot, SyntaxNode modifiedRoot)
	{

		var originalNodesMap = originalRoot.DescendantNodesAndSelf()
			.Where(n => !IsWhitespaceOrTrivia(n))
			.ToLookup(n => n.GetOptimizedNodeHash());

		var modifiedNodesMap = modifiedRoot.DescendantNodesAndSelf()
			.Where(n => !IsWhitespaceOrTrivia(n))
			.ToLookup(n => n.GetOptimizedNodeHash());

		var result = new SyntaxTreeComparison();


		foreach (var modNode in modifiedRoot.DescendantNodesAndSelf())
		{
			if (IsWhitespaceOrTrivia(modNode)) continue;

			if (!originalNodesMap.Contains(modNode.GetOptimizedNodeHash()))
			{
				// Se o pai dele também for um novo nó, evitamos redundância na lista principal
				if (!IsParentAlreadyAdded(modNode, originalNodesMap))
				{
					result.AddedNodes.Add(modNode);
				}
			}
		}

		// 3. Encontrar nós REMOVIDOS (existem na original, mas não na modificada)
		foreach (var origNode in originalRoot.DescendantNodesAndSelf())
		{
			if (IsWhitespaceOrTrivia(origNode)) continue;

			if (!modifiedNodesMap.Contains(origNode.GetOptimizedNodeHash()))
			{
				if (!IsParentAlreadyRemoved(origNode, modifiedNodesMap))
				{
					result.RemovedNodes.Add(origNode);
				}
			}
		}
		var origPositionalMap = originalRoot.DescendantNodes()
			.ToDictionary(n => $"{n.RawKind}_{n.SpanStart}");

		foreach (var modNode in modifiedRoot.DescendantNodes())
		{
			string key = $"{modNode.RawKind}_{modNode.SpanStart}";
			if (origPositionalMap.TryGetValue(key, out var origNode))
			{
				// Se o hash mudou mas a posição/tipo é igual, o conteúdo foi alterado
				if (origNode.GetOptimizedNodeHash() != modNode.GetOptimizedNodeHash())
				{
					// Verifica se não é apenas uma mudança propagada de um filho
					if (!origNode.IsEquivalentTo(modNode))
					{
						result.ModifiedNodes.Add(new SyntaxNodeChange
						{
							OriginalNode = origNode,
							ModifiedNode = modNode,
							ChangeType = DetermineNodeChangeType(origNode, modNode)
						});
					}
				}
			}
		}

		return result;
	}

	private bool IsWhitespaceOrTrivia(SyntaxNode node)
	{
		return node is DocumentationCommentTriviaSyntax || node.IsMissing;
	}

	private bool IsParentAlreadyAdded(SyntaxNode node, ILookup<string, SyntaxNode> originalMap)
	{
		var parent = node.Parent;
		while (parent != null)
		{
			if (!originalMap.Contains(parent.GetOptimizedNodeHash())) return true;
			parent = parent.Parent;
		}
		return false;
	}

	private bool IsParentAlreadyRemoved(SyntaxNode node, ILookup<string, SyntaxNode> modifiedMap)
	{
		var parent = node.Parent;
		while (parent != null)
		{
			if (!modifiedMap.Contains(parent.GetOptimizedNodeHash())) return true;
			parent = parent.Parent;
		}
		return false;
	}

	private string DetermineNodeChangeType(SyntaxNode original, SyntaxNode modified)
	{
		if (original.RawKind != modified.RawKind) return "KindMismatch";
		return original.ToString() != modified.ToString() ? "ContentChanged" : "InternalStructureChanged";
	}
}