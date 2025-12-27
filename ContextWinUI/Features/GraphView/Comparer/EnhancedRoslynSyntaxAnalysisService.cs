using ContextWinUI.Features.GraphView;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;
public class EnhancedRoslynSyntaxAnalysisService : RoslynSyntaxAnalysisService
{
	private readonly ISnippetFileRelationService _relationService;
	private readonly IRoslynTreeDiffer _treeDiffer;

	public EnhancedRoslynSyntaxAnalysisService(
		ISnippetFileRelationService relationService,
		IRoslynTreeDiffer treeDiffer)
	{
		_relationService = relationService;
		_treeDiffer = treeDiffer;
	}

	public async Task<ComparisonResult> AnalyzeSnippetChangesAsync(string snippet, string fileContent, string filePath)
	{
		return await _relationService.CompareSnippetWithFileAsync(snippet, fileContent, filePath);
	}

	public async Task<SyntaxTreeComparison> CompareSyntaxTreesAsync(SyntaxTree originalTree, SyntaxTree modifiedTree)
	{
		var originalRoot = await originalTree.GetRootAsync();
		var modifiedRoot = await modifiedTree.GetRootAsync();
		return _treeDiffer.Compare(originalRoot, modifiedRoot);
	}
}