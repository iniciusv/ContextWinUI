using ContextWinUI.Core.Contracts;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Services;

public class SymbolResolutionService: ISymbolResolutionService
{
	private readonly ISemanticIndexService _indexService;

	public SymbolResolutionService(ISemanticIndexService indexService)
	{
		_indexService = indexService;
	}

	public async Task<Models.SymbolResolutionResult?> ResolveSymbolAtPositionAsync(string text, int cursorIndex, int blockStartPos, string filePath)
	{
		string word = GetWordAtCursor(text, cursorIndex);
		if (string.IsNullOrEmpty(word)) return null;

		int absPos = blockStartPos + cursorIndex;
		
        // Agora usamos await corretamente, sem bloquear a thread de UI
        var roslynNode = await _indexService.ResolveSymbolWithRoslynAsync(filePath, absPos);
        var node = roslynNode ?? _indexService.InferSymbolFromGraph(word, filePath, absPos);

		if (node != null)
		{
			var graph = _indexService.GetCurrentGraph();
			string resolvedPath = graph.GetFilePath(node.FileId);
            
            if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
            {
                return new Models.SymbolResolutionResult(
                    node.Name,
                    node.Type.ToString(),
                    resolvedPath,
                    node.StartPosition,
                    false 
                );
            }
		}

		return null;
	}

	public async Task<List<Models.SymbolResolutionResult>> FindImplementationsAtPositionAsync(string text, int cursorIndex, int blockStartPos, string filePath)
    {
        var results = new List<Models.SymbolResolutionResult>();
        
        int absPos = blockStartPos + cursorIndex;
        var nodes = await _indexService.FindImplementationsWithRoslynAsync(filePath, absPos);

        if (nodes != null)
        {
            var graph = _indexService.GetCurrentGraph();
            foreach (var node in nodes)
            {
                string resolvedPath = graph.GetFilePath(node.FileId);
                if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
                {
                    results.Add(new Models.SymbolResolutionResult(
                        node.Name,
                        node.Type.ToString(),
                        resolvedPath,
                        node.StartPosition,
                        true
                    ));
                }
            }
        }

        return results;
    }

	private string GetWordAtCursor(string text, int position)
	{
		if (string.IsNullOrEmpty(text) || position < 0 || position > text.Length) return string.Empty;

		int start = position;
		int end = position;

		while (start > 0 && start <= text.Length && IsIdentifierChar(text[start - 1])) start--;
		while (end < text.Length && IsIdentifierChar(text[end])) end++;

		if (start >= end) return string.Empty;

		return text.Substring(start, end - start);
	}

	private bool IsIdentifierChar(char c)
	{
		return char.IsLetterOrDigit(c) || c == '_';
	}
}