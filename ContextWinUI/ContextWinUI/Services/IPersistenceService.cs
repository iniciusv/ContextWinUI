using ContextWinUI.ContextWinUI.Models;
using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IPersistenceService
{
	Task SaveProjectCacheAsync(string projectRootPath, IEnumerable<FileSharedState> states, string prePrompt, bool omitUsings, bool omitComments);
	Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath);
}