using ContextWinUI.Core.Contracts;
using System.IO;

namespace ContextWinUI.Features.GraphParser.Services;

public class SymbolResolutionService: ISymbolResolutionService
{
	private readonly ISemanticIndexService _indexService;

	public SymbolResolutionService(ISemanticIndexService indexService)
	{
		_indexService = indexService;
	}

	public string? ResolveSymbolAtPosition(string text, int cursorIndex, int blockStartPos, string filePath)
	{
		string word = GetWordAtCursor(text, cursorIndex);
		if (string.IsNullOrEmpty(word)) return null;

		int absPos = blockStartPos + cursorIndex;
		var node = _indexService.InferSymbolFromGraph(word, filePath, absPos);

		if (node != null)
		{
			var graph = _indexService.GetCurrentGraph();
			string resolvedPath = graph.GetFilePath(node.FileId);
			string fileName = string.IsNullOrEmpty(resolvedPath) ? "Desconhecido" : Path.GetFileName(resolvedPath);
			return $"[{node.Type}] {node.Name}\nDefinido em: {fileName}";
		}

		return $"'{word}' (Sem informações no grafo)";
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