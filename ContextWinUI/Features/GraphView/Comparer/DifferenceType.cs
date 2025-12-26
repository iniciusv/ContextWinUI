using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI;

public interface ISnippetFileRelationService
{
	Task<ComparisonResult> CompareSnippetWithFileAsync(
		string snippet,
		string fileContent,
		string filePath);

	Task<MatchAnalysis> FindBestMatchInFileAsync(
		string snippet,
		string fileContent,
		string filePath);
}

public enum ChangeType
{
	Unchanged,
	Inserted,
	Removed,
	Modified,
	Moved
}

public class TokenChange
{
	public SymbolNode OriginalToken { get; set; }
	public SymbolNode? NewToken { get; set; }
	public ChangeType ChangeType { get; set; }
	public double SimilarityScore { get; set; }
	public string? ChangeDescription { get; set; }
}

public class ScopeMatch
{
	public SymbolNode FileScope { get; set; }
	public SymbolNode SnippetScope { get; set; }
	public double SimilarityScore { get; set; }
	public List<TokenChange> TokenChanges { get; set; } = new();
	public bool IsPartialMatch { get; set; }
}

public class ComparisonResult
{
	public List<ScopeMatch> ScopeMatches { get; set; } = new();
	public List<TokenChange> UnmatchedTokens { get; set; } = new();
	public double OverallSimilarity { get; set; }
	public string? BestMatchLocation { get; set; }
	public List<string> SuggestedChanges { get; set; } = new();
}

public class MatchAnalysis
{
	public int StartLine { get; set; }
	public int EndLine { get; set; }
	public double Confidence { get; set; }
	public List<ScopeMatch> MatchingScopes { get; set; } = new();
	public string? ContextBefore { get; set; }
	public string? ContextAfter { get; set; }
}

public class TokenChangeViewModel : ObservableObject
{
	public ChangeType ChangeType { get; set; }
	public string Description { get; set; } = string.Empty;
	public Color ChangeTypeColor { get; set; }
	public Color TextColor { get; set; }
	public string ChangeTypeIcon { get; set; } = string.Empty;
}