using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Algorithms;

public class SemanticDiffService
{
	public IEnumerable<TextSpan> GetSemanticChanges(string oldContent, string newContent)
	{
		if (string.IsNullOrEmpty(oldContent) || string.IsNullOrEmpty(newContent))
			return Enumerable.Empty<TextSpan>();

		var oldTokens = GetSignificantTokens(oldContent);
		var newTokens = GetSignificantTokens(newContent);

		return CalculateDiffSpans(oldTokens, newTokens);
	}

	private List<SyntaxToken> GetSignificantTokens(string code)
	{
		var tree = CSharpSyntaxTree.ParseText(code);
		return tree.GetRoot()
				   .DescendantTokens()
				   .Where(t => t.Kind() != SyntaxKind.EndOfFileToken)
				   .ToList();
	}

	private IEnumerable<TextSpan> CalculateDiffSpans(List<SyntaxToken> oldList, List<SyntaxToken> newList)
	{
		int m = oldList.Count;
		int n = newList.Count;
		int[,] dp = new int[m + 1, n + 1];

		for (int i = 1; i <= m; i++)
		{
			for (int j = 1; j <= n; j++)
			{
				if (AreTokensEquivalent(oldList[i - 1], newList[j - 1]))
				{
					dp[i, j] = dp[i - 1, j - 1] + 1;
				}
				else
				{
					dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
				}
			}
		}

		var changes = new List<TextSpan>();
		int x = m, y = n;

		while (y > 0)
		{
			if (x > 0 && AreTokensEquivalent(oldList[x - 1], newList[y - 1]))
			{
				x--;
				y--;
			}
			else if (x > 0 && dp[x - 1, y] >= dp[x, y - 1])
			{
				x--;
			}
			else
			{
				changes.Add(newList[y - 1].Span);
				y--;
			}
		}

		return changes;
	}

	private bool AreTokensEquivalent(SyntaxToken t1, SyntaxToken t2)
	{
		return t1.RawKind == t2.RawKind && t1.Text == t2.Text;
	}
}
