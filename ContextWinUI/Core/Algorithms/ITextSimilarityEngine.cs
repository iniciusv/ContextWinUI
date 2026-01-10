namespace ContextWinUI.Core.Algorithms;

public interface ITextSimilarityEngine
{
	double CalculateSimilarity(string source, string target);
}
