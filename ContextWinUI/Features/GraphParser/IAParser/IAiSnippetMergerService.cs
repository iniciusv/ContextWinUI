// ARQUIVO: IAiSnippetMergerService.cs
using ContextWinUI.Features.GraphParser.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.IAParser;


public interface IAiSnippetMergerService
{
	/// <summary>
	/// Analisa um snippet, encontra o arquivo correspondente no projeto,
	/// e injeta o código como novas versões nos blocos existentes.
	/// </summary>
	Task<MergeResult> MergeSnippetAsync(string snippetContent);
}

public class MergeResult
{
	public bool Success { get; set; }
	public string TargetFilePath { get; set; } = string.Empty;
	public List<CodeBlockItem> UpdatedBlocks { get; set; } = new();
	public int BlocksModified { get; set; }
	public int NewBlocksAdded { get; set; }
	public string Message { get; set; } = string.Empty;
}