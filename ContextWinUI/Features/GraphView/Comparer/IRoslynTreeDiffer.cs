using Microsoft.CodeAnalysis;

namespace ContextWinUI.Features.GraphView;

public interface IRoslynTreeDiffer
{
	SyntaxTreeComparison Compare(SyntaxNode originalRoot, SyntaxNode modifiedRoot);
}