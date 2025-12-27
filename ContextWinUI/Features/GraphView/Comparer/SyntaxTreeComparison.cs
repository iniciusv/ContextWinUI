using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;

public class SyntaxTreeComparison
{
	public List<SyntaxNode> AddedNodes { get; set; } = new();
	public List<SyntaxNode> RemovedNodes { get; set; } = new();
	public List<SyntaxNodeChange> ModifiedNodes { get; set; } = new();
}
public class SyntaxNodeChange

{
	public SyntaxNode OriginalNode { get; set; }
	public SyntaxNode ModifiedNode { get; set; }
	public string ChangeType { get; set; }
}
