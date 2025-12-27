using System.Collections.Generic;

namespace ContextWinUI.Features.GraphView;

public interface ICodeConsolidationService
{
	string MergeMultipleContexts(string originalContent, string fullSnippet, IEnumerable<ContextActionViewModel> actions);
}