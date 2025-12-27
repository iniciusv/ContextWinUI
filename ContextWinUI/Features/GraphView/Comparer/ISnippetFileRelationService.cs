using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphView;


public interface ISnippetFileRelationService
{
	Task<ComparisonResult> CompareSnippetWithFileAsync(
		string snippet,
		string fileContent,
		string filePath);

	Task<MatchAnalysis> FindBestMatchInFileAsync(
		string snippet,
		string fileContent,
		string filePath);
}
