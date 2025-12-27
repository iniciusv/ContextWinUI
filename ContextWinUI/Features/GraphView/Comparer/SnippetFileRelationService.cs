using ContextWinUI.Core.Algorithms;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphView;
using ContextWinUI.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;

public class SnippetFileRelationService : ISnippetFileRelationService
{
	private readonly ISyntaxAnalysisService _syntaxAnalysisService;
	private readonly ITokenDiffEngine _diffEngine;
	private readonly ITextSimilarityEngine _similarityEngine;

	public SnippetFileRelationService(
		ISyntaxAnalysisService syntaxAnalysisService,
		ITokenDiffEngine diffEngine,
		ITextSimilarityEngine similarityEngine)
	{
		_syntaxAnalysisService = syntaxAnalysisService;
		_diffEngine = diffEngine;
		_similarityEngine = similarityEngine;
	}

	public async Task<ComparisonResult> CompareSnippetWithFileAsync(string snippet, string fileContent, string filePath)
	{
		// 1. ANÁLISE SINTÁTICA: Extrai árvores, escopos e tokens de ambos
		var fileAnalysis = await _syntaxAnalysisService.AnalyzeFileAsync(fileContent, filePath);
		var snippetAnalysis = await _syntaxAnalysisService.AnalyzeSnippetAsync(snippet);

		// 2. PAREAMENTO DE ESCOPOS: Identifica quais métodos/classes do snippet 
		// correspondem a quais no arquivo original
		var scopeMatches = AnalyzeScopeMatches(fileAnalysis.Scopes, snippetAnalysis.Scopes);

		foreach (var match in scopeMatches)
		{
			// 3. EXTRAÇÃO DE TOKENS: Busca apenas os tokens contidos dentro dos limites deste escopo
			var fileScopeTokens = GetTokensInScopeOptimized(fileAnalysis.Tokens, match.FileScope);
			var snippetScopeTokens = GetTokensInScopeOptimized(snippetAnalysis.Tokens, match.SnippetScope);

			// 4. DIFF GRANULAR: Delega ao motor de Diff a tarefa de encontrar as mudanças exatas
			// Passamos o predicado 'AreTokensSimilar' que usa a engine de similaridade de texto
			match.TokenChanges = _diffEngine.ComputeDiff(
				fileScopeTokens,
				snippetScopeTokens,
				(t1, t2) => AreTokensSimilar(t1, t2)
			);

			// 5. PONTUAÇÃO: Calcula quão similar o escopo ficou após o Diff
			match.SimilarityScore = CalculateScopeSimilarity(match.TokenChanges);
			match.IsPartialMatch = match.SimilarityScore > AnalysisConstants.MinScopeSimilarityThreshold;
		}

		// 6. RESULTADO FINAL: Agrega todas as análises em um DTO de resultado
		return new ComparisonResult
		{
			ScopeMatches = scopeMatches.Where(m => m.IsPartialMatch).ToList(),
			OverallSimilarity = CalculateOverallSimilarity(scopeMatches),
			BestMatchLocation = FindBestMatchLocation(scopeMatches, fileContent),
			UnmatchedTokens = FindUnmatchedTokens(fileAnalysis.Tokens, snippetAnalysis.Tokens, scopeMatches),
			SuggestedChanges = GenerateChangeSuggestions(scopeMatches)
		};
	}

	private bool AreTokensSimilar(SymbolNode t1, SymbolNode t2)
	{
		// Se o tipo do token for diferente (ex: uma variável vs um método), não são similares
		if (t1.Type != t2.Type) return false;

		// Se for um identificador nomeável, usamos a Engine de Similaridade (Fuzzy Match)
		if (t1.Type == SymbolType.LocalVariable || t1.Type == SymbolType.Method || t1.Type == SymbolType.Parameter)
		{
			return _similarityEngine.CalculateSimilarity(t1.Name, t2.Name)
				   > AnalysisConstants.FuzzyTokenSimilarityThreshold;
		}

		// Para palavras-chave ou operadores, a comparação deve ser exata
		return t1.Name == t2.Name;
	}

	private List<ScopeMatch> AnalyzeScopeMatches(List<SymbolNode> fileScopes, List<SymbolNode> snippetScopes)
	{
		var matches = new List<ScopeMatch>();

		foreach (var sScope in snippetScopes)
		{
			// Tenta encontrar o melhor candidato no arquivo para este escopo do snippet
			var bestMatch = fileScopes
				.Select(fScope => new ScopeMatch
				{
					FileScope = fScope,
					SnippetScope = sScope,
					// Cálculo inicial baseado apenas no nome e tipo do escopo
					SimilarityScore = CalculateInitialScopeScore(fScope, sScope)
				})
				.OrderByDescending(m => m.SimilarityScore)
				.FirstOrDefault();

			if (bestMatch != null && bestMatch.SimilarityScore > 0.2) // Threshold mínimo de pareamento
			{
				matches.Add(bestMatch);
			}
		}

		return matches;
	}

	private double CalculateInitialScopeScore(SymbolNode fScope, SymbolNode sScope)
	{
		if (fScope.Type != sScope.Type) return 0;

		double score = _similarityEngine.CalculateSimilarity(fScope.Name, sScope.Name);

		// Bônus se os nomes forem idênticos
		if (fScope.Name == sScope.Name) score += AnalysisConstants.NameMatchBonus;

		return Math.Min(score, 1.0);
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

	private double CalculateScopeSimilarity(List<TokenChange> changes) => changes.Count == 0 ? 0 : (double)changes.Count(c => c.ChangeType == ChangeType.Unchanged) / changes.Count;
	private double CalculateOverallSimilarity(List<ScopeMatch> matches) => matches.Count == 0 ? 0 : matches.Average(m => m.SimilarityScore);
	public async Task<MatchAnalysis> FindBestMatchInFileAsync(string snippet, string fileContent, string filePath) { /* ... */ return new MatchAnalysis(); }
	private string? FindBestMatchLocation(List<ScopeMatch> m, string c) => null;
	private List<TokenChange> FindUnmatchedTokens(List<SymbolNode> ft, List<SymbolNode> st, List<ScopeMatch> sm) => new List<TokenChange>();
	private List<string> GenerateChangeSuggestions(List<ScopeMatch> sm) => new List<string>();
}