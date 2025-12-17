using System.Collections.Generic;
using System.Threading.Tasks;
using static ContextWinUI.Services.RoslynAnalyzerService;
namespace ContextWinUI.Services;

public interface IRoslynAnalyzerService
{
	Task IndexProjectAsync(string rootPath);
	Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath);
	Task<List<string>> GetMethodCallsAsync(string filePath, string methodSignature);
}