using ContextWinUI.Core.Models;
using System;
using System.Collections.Generic;

namespace ContextWinUI.Features.GraphView;

public class TokenDiffEngine : ITokenDiffEngine
{
	public List<TokenChange> ComputeDiff(
		List<SymbolNode> source,
		List<SymbolNode> target,
		Func<SymbolNode, SymbolNode, bool> similarityPredicate)
	{
		// 1. Guardrail de Memória
		long matrixSize = (long)(source.Count + 1) * (target.Count + 1);
		if (matrixSize > AnalysisConstants.MaxMatrixElements)
		{
			return CreatePerformanceLimitResult();
		}

		// 2. Cálculo da Matriz (LCS - Longest Common Subsequence)
		int[,] dp = BuildDiffMatrix(source, target, similarityPredicate);

		// 3. Backtracking para gerar a lista de mudanças
		return BacktrackChanges(dp, source, target, similarityPredicate);
	}

	private int[,] BuildDiffMatrix(List<SymbolNode> source, List<SymbolNode> target, Func<SymbolNode, SymbolNode, bool> similarityPredicate)
	{
		int[,] dp = new int[source.Count + 1, target.Count + 1];
		for (int i = 1; i <= source.Count; i++)
		{
			for (int j = 1; j <= target.Count; j++)
			{
				if (similarityPredicate(source[i - 1], target[j - 1]))
					dp[i, j] = dp[i - 1, j - 1] + 1;
				else
					dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
			}
		}
		return dp;
	}

	private List<TokenChange> BacktrackChanges(int[,] dp, List<SymbolNode> source, List<SymbolNode> target, Func<SymbolNode, SymbolNode, bool> similarityPredicate)
	{
		var changes = new List<TokenChange>();
		int x = source.Count, y = target.Count;

		while (x > 0 || y > 0)
		{
			if (x > 0 && y > 0 && similarityPredicate(source[x - 1], target[y - 1]))
			{
				changes.Add(new TokenChange { OriginalToken = source[x - 1], NewToken = target[y - 1], ChangeType = ChangeType.Unchanged, SimilarityScore = 1.0 });
				x--; y--;
			}
			else if (y > 0 && (x == 0 || dp[x, y - 1] >= dp[x - 1, y]))
			{
				changes.Add(new TokenChange { NewToken = target[y - 1], ChangeType = ChangeType.Inserted, ChangeDescription = $"Inserido: {target[y - 1].Name}" });
				y--;
			}
			else
			{
				changes.Add(new TokenChange { OriginalToken = source[x - 1], ChangeType = ChangeType.Removed, ChangeDescription = $"Removido: {source[x - 1].Name}" });
				x--;
			}
		}
		changes.Reverse();
		return changes;
	}

	private List<TokenChange> CreatePerformanceLimitResult() => new() {
		new TokenChange { ChangeType = ChangeType.Modified, ChangeDescription = "Escopo muito grande para análise granular." }
	};
}