// ARQUIVO: AnalysisResult.cs
using ContextWinUI.Core.Models;
using System.Collections.Generic;

namespace ContextWinUI.Features.GraphView;

public class AnalysisResult
{
	public List<SymbolNode> Scopes { get; }
	public List<SymbolNode> Tokens { get; }

	public AnalysisResult(List<SymbolNode> scopes, List<SymbolNode> tokens)
	{
		Scopes = scopes;
		Tokens = tokens;
	}

	public static AnalysisResult Empty() => new(new(), new());
}