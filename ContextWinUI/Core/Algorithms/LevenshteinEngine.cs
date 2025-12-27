using System;

namespace ContextWinUI.Core.Algorithms;

public interface ITextSimilarityEngine
{
	double CalculateSimilarity(string source, string target);
}

public class LevenshteinEngine : ITextSimilarityEngine
{
	public double CalculateSimilarity(string source, string target)
	{
		if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return 0;
		if (source == target) return 1.0;

		int distance = ComputeLevenshteinDistance(source, target);
		return 1.0 - (double)distance / Math.Max(source.Length, target.Length);
	}

	private int ComputeLevenshteinDistance(string a, string b)
	{
		var dp = new int[a.Length + 1, b.Length + 1];
		for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
		for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

		for (int i = 1; i <= a.Length; i++)
		{
			for (int j = 1; j <= b.Length; j++)
			{
				int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
				dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
			}
		}
		return dp[a.Length, b.Length];
	}
}