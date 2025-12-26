using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphView;
using ContextWinUI.Features.GraphView.Comparer;
using ContextWinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class SnippetFileRelationService : ISnippetFileRelationService
{
	private readonly ISyntaxAnalysisService _syntaxAnalysisService;

	public SnippetFileRelationService(ISyntaxAnalysisService syntaxAnalysisService)
	{
		_syntaxAnalysisService = syntaxAnalysisService;
	}

	public async Task<ComparisonResult> CompareSnippetWithFileAsync(string snippet, string fileContent, string filePath)
	{
		var fileAnalysis = await _syntaxAnalysisService.AnalyzeFileAsync(fileContent, filePath);
		var snippetAnalysis = await _syntaxAnalysisService.AnalyzeSnippetAsync(snippet);

		var scopeMatches = AnalyzeScopeMatches(fileAnalysis.Scopes, snippetAnalysis.Scopes);

		foreach (var match in scopeMatches)
		{
			var fileScopeTokens = GetTokensInScopeOptimized(fileAnalysis.Tokens, match.FileScope);
			var snippetScopeTokens = GetTokensInScopeOptimized(snippetAnalysis.Tokens, match.SnippetScope);

			match.TokenChanges = AnalyzeTokenChanges(fileScopeTokens, snippetScopeTokens);
			match.SimilarityScore = CalculateScopeSimilarity(match.TokenChanges);
		}

		return new ComparisonResult
		{
			ScopeMatches = scopeMatches,
			OverallSimilarity = CalculateOverallSimilarity(scopeMatches),
			BestMatchLocation = FindBestMatchLocation(scopeMatches, fileContent),
			UnmatchedTokens = FindUnmatchedTokens(fileAnalysis.Tokens, snippetAnalysis.Tokens, scopeMatches),
			SuggestedChanges = GenerateChangeSuggestions(scopeMatches)
		};
	}

	private List<SymbolNode> GetTokensInScopeOptimized(List<SymbolNode> allTokens, SymbolNode scope)
	{
		var result = new List<SymbolNode>();
		int endPos = scope.StartPosition + scope.Length;
		int startIndex = allTokens.BinarySearchFirstOccurrence(scope.StartPosition);

		if (startIndex == -1) return result;

		for (int i = startIndex; i < allTokens.Count; i++)
		{
			var token = allTokens[i];
			if (token.StartPosition >= endPos) break;
			if (token.StartPosition >= scope.StartPosition && (token.StartPosition + token.Length <= endPos))
				result.Add(token);
		}
		return result;
	}

	private List<TokenChange> AnalyzeTokenChanges(List<SymbolNode> fileTokens, List<SymbolNode> snippetTokens)
	{
		// OTIMIZAÇÃO DE MEMÓRIA (Guardrail)
		// Se a matriz for muito grande, aborta a comparação granular detalhada para evitar OutOfMemory ou travamento de CPU.
		long matrixSize = (long)(fileTokens.Count + 1) * (snippetTokens.Count + 1);
		if (matrixSize > AnalysisConstants.MaxMatrixElements)
		{
			return new List<TokenChange>
			{
				new TokenChange
				{
					ChangeType = ChangeType.Modified,
					ChangeDescription = "Escopo muito grande para análise granular (Performance Limit)."
				}
			};
		}

		var changes = new List<TokenChange>();
		int[,] dp = new int[fileTokens.Count + 1, snippetTokens.Count + 1];

		// Lógica DP original mantida...
		for (int i = 1; i <= fileTokens.Count; i++)
		{
			for (int j = 1; j <= snippetTokens.Count; j++)
			{
				if (AreTokensSimilar(fileTokens[i - 1], snippetTokens[j - 1]))
					dp[i, j] = dp[i - 1, j - 1] + 1;
				else
					dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
			}
		}

		// Backtracking original mantido...
		int x = fileTokens.Count, y = snippetTokens.Count;
		while (x > 0 || y > 0)
		{
			if (x > 0 && y > 0 && AreTokensSimilar(fileTokens[x - 1], snippetTokens[y - 1]))
			{
				changes.Add(new TokenChange
				{
					OriginalToken = fileTokens[x - 1],
					NewToken = snippetTokens[y - 1],
					ChangeType = ChangeType.Unchanged,
					SimilarityScore = 1.0
				});
				x--; y--;
			}
			else if (y > 0 && (x == 0 || dp[x, y - 1] >= dp[x - 1, y]))
			{
				changes.Add(new TokenChange
				{
					NewToken = snippetTokens[y - 1],
					ChangeType = ChangeType.Inserted,
					SimilarityScore = 0,
					ChangeDescription = $"Inserido: {snippetTokens[y - 1].Name}"
				});
				y--;
			}
			else if (x > 0 && (y == 0 || dp[x, y - 1] < dp[x - 1, y]))
			{
				changes.Add(new TokenChange
				{
					OriginalToken = fileTokens[x - 1],
					ChangeType = ChangeType.Removed,
					SimilarityScore = 0,
					ChangeDescription = $"Removido: {fileTokens[x - 1].Name}"
				});
				x--;
			}
		}
		changes.Reverse();
		return changes;
	}

	private bool AreTokensSimilar(SymbolNode token1, SymbolNode token2)
	{
		if (token1.Type != token2.Type) return false;

		if (token1.Type == SymbolType.LocalVariable ||
			token1.Type == SymbolType.Parameter ||
			token1.Type == SymbolType.Method)
		{
			// USO DE CONSTANTE
			return CalculateTokenSimilarity(token1.Name, token2.Name) > AnalysisConstants.FuzzyTokenSimilarityThreshold;
		}
		return token1.Name == token2.Name;
	}


	private double CalculateTokenSimilarity(string str1, string str2)
	{
		if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
			return 0;

		int maxLen = Math.Max(str1.Length, str2.Length);
		int distance = LevenshteinDistance(str1, str2);

		return 1.0 - (double)distance / maxLen;
	}
	private int LevenshteinDistance(string a, string b)
	{
		if (Math.Abs(a.Length - b.Length) > 50) return 100;

		var dp = new int[a.Length + 1, b.Length + 1];

		for (int i = 0; i <= a.Length; i++)
			dp[i, 0] = i;

		for (int j = 0; j <= b.Length; j++)
			dp[0, j] = j;

		for (int i = 1; i <= a.Length; i++)
		{
			for (int j = 1; j <= b.Length; j++)
			{
				int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
				dp[i, j] = Math.Min(
					Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
					dp[i - 1, j - 1] + cost);
			}
		}

		return dp[a.Length, b.Length];
	}

	private double CalculateScopeSimilarity(List<TokenChange> changes) => changes.Count == 0 ? 0 : (double)changes.Count(c => c.ChangeType == ChangeType.Unchanged) / changes.Count;
	private double CalculateOverallSimilarity(List<ScopeMatch> matches) => matches.Count == 0 ? 0 : matches.Average(m => m.SimilarityScore);

	public async Task<MatchAnalysis> FindBestMatchInFileAsync(string snippet, string fileContent, string filePath) { /* ... */ return new MatchAnalysis(); }
	private List<ScopeMatch> AnalyzeScopeMatches(List<SymbolNode> fs, List<SymbolNode> ss) => new List<ScopeMatch>();
	private string? FindBestMatchLocation(List<ScopeMatch> m, string c) => null;
	private List<TokenChange> FindUnmatchedTokens(List<SymbolNode> ft, List<SymbolNode> st, List<ScopeMatch> sm) => new List<TokenChange>();
	private List<string> GenerateChangeSuggestions(List<ScopeMatch> sm) => new List<string>();
}