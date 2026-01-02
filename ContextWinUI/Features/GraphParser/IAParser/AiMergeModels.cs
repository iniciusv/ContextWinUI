// ARQUIVO: AiMergeModels.cs
namespace ContextWinUI.Features.GraphParser.IAParser;

using ContextWinUI.Features.GraphParser.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

public class AiMergeResult
{
	public bool Success { get; set; }
	public string FilePath { get; set; } = string.Empty;
	public List<CodeBlockItem> MergedBlocks { get; set; } = new();
	public string Message { get; set; } = string.Empty;
	public int ModifiedCount { get; set; }
	public int AddedCount { get; set; }
}

public interface IAiCodeMerger
{
	/// <summary>
	/// Processa o snippet, identifica o arquivo alvo, carrega-o do disco e realiza o merge dos blocos.
	/// </summary>
	Task<AiMergeResult> MergeAiSnippetAsync(string snippetCode);
}