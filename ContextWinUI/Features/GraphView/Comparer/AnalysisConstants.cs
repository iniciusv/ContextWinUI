namespace ContextWinUI.Features.GraphView.Comparer;

public static class AnalysisConstants
{
	public const double MinScopeSimilarityThreshold = 0.3;
	public const double FuzzyTokenSimilarityThreshold = 0.6;
	public const double NameMatchBonus = 0.4;
	public const double ParentMatchBonus = 0.3;

	public const int MaxMatrixElements = 2_000_000; 
}