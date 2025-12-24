using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IPersistenceService
{
	Task SaveProjectCacheAsync(
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders,
		Dictionary<string, string> tagColors);

	Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath);
}