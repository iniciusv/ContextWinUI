using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		bool omitComments,
		bool includeStructure,     
		bool structureOnlyFolders);

	Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath);
}