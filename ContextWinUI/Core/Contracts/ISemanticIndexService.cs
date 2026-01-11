using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface ISemanticIndexService
{
	Task<DependencyGraph> GetOrIndexProjectAsync(string rootPath);

	DependencyGraph GetCurrentGraph();

	SymbolNode? InferSymbolFromGraph(string word, string filePath, int absolutePosition);

	SymbolType? GetSymbolType(string word);
	Task UpdateSourceFileAsync(string filePath, string newContent);

	Task ReloadFilesAsync(IEnumerable<string> filePaths);
	Task<string?> GetSourceContentAsync(string filePath);
}