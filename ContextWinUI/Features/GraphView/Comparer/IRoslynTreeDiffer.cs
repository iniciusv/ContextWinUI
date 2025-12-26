using Microsoft.CodeAnalysis;

namespace ContextWinUI.Features.GraphView.Comparer;

public interface IRoslynTreeDiffer
{
	SyntaxTreeComparison Compare(SyntaxNode originalRoot, SyntaxNode modifiedRoot);
}