using ContextWinUI.Services;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface ILanguageStrategy
{
	bool CanHandle(string extension);

	Task<RoslynAnalyzerService.FileAnalysisResult> AnalyzeAsync(string filePath);
}
