using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView
{
	public interface ISyntaxAnalysisService
	{
		Task<AnalysisResult> AnalyzeFileAsync(string code, string filePath);
		Task<AnalysisResult> AnalyzeSnippetAsync(string snippetCode);
	}
}