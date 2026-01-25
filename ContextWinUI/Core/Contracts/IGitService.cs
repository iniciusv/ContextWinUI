using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IGitService
{
	bool IsGitRepository(string rootPath);

	Task<IEnumerable<(string Path, bool IsDeleted)>> GetModifiedFilesAsync(string rootPath);
    
    // New methods for History Feature
    Task<IEnumerable<ContextWinUI.Core.Models.GitCommitItem>> GetCommitHistoryAsync(string rootPath, int limit = 50);
    Task<string?> GetFileContentAtCommitAsync(string rootPath, string filePath, string sha);
    Task<IEnumerable<(string Path, bool IsDeleted)>> GetChangesBetweenCommitsAsync(string rootPath, string oldSha, string newSha);

	Task<string?> GetFileContentFromHeadAsync(string rootPath, string filePath);
}