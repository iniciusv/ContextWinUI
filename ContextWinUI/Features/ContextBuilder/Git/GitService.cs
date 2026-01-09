using ContextWinUI.Core.Contracts;
using LibGit2Sharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class GitService : IGitService
{
	public bool IsGitRepository(string rootPath)
	{
		return Repository.IsValid(rootPath);
	}

	public Task<IEnumerable<string>> GetModifiedFilesAsync(string rootPath)
	{
		return Task.Run(() =>
		{
			var modifiedFiles = new List<string>();
			if (!Repository.IsValid(rootPath))
				return Enumerable.Empty<string>();

			using (var repo = new Repository(rootPath))
			{
				var status = repo.RetrieveStatus(new StatusOptions
				{
					IncludeUntracked = true
				});

				foreach (var item in status)
				{
					if (item.State == FileStatus.ModifiedInIndex ||
						item.State == FileStatus.ModifiedInWorkdir ||
						item.State == FileStatus.NewInIndex ||
						item.State == FileStatus.NewInWorkdir ||
						item.State == FileStatus.RenamedInIndex ||
						item.State == FileStatus.RenamedInWorkdir ||
						item.State == FileStatus.DeletedFromIndex ||   // <--- Novo
						item.State == FileStatus.DeletedFromWorkdir)   // <--- Novo
					{
						var fullPath = Path.Combine(rootPath, item.FilePath);
						modifiedFiles.Add(fullPath.Replace("/", "\\"));
					}
				}
			}
			return (IEnumerable<string>)modifiedFiles;
		});
	}
}