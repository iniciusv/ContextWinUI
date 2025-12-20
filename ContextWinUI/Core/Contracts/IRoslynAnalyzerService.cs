using ContextWinUI.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using static ContextWinUI.Features.CodeAnalyses.RoslynAnalyzerService;

namespace ContextWinUI.Core.Contracts;

public interface IRoslynAnalyzerService
{
	Task IndexProjectAsync(string rootPath);
	Task<FileAnalysisResult> AnalyzeFileStructureAsync(string filePath);
	Task<List<string>> GetMethodCallsAsync(string filePath, string methodSignature);
	Task<string> FilterClassContentAsync(string filePath,IEnumerable<string> keptMethodSignatures,bool removeUsings,bool removeNamespaces,bool removeComments,bool removeEmptyLines);
	Task<MethodBodyResult> AnalyzeMethodBodyAsync(string filePath, string methodSignature);
}