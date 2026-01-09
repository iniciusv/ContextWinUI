using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IGitService
{
	bool IsGitRepository(string rootPath);

	Task<IEnumerable<(string Path, bool IsDeleted)>> GetModifiedFilesAsync(string rootPath);
}