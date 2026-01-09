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

	public Task<IEnumerable<(string Path, bool IsDeleted)>> GetModifiedFilesAsync(string rootPath)
	{
		return Task.Run(() =>
		{
			var modifiedFiles = new List<(string Path, bool IsDeleted)>();

			if (!Repository.IsValid(rootPath))
				return Enumerable.Empty<(string, bool)>();

			using (var repo = new Repository(rootPath))
			{
				var status = repo.RetrieveStatus(new StatusOptions
				{
					IncludeUntracked = true
				});

				foreach (var item in status)
				{
					// Verifica status de deleção
					bool isDeleted = item.State == FileStatus.DeletedFromIndex ||
									 item.State == FileStatus.DeletedFromWorkdir;

					if (item.State == FileStatus.ModifiedInIndex ||
						item.State == FileStatus.ModifiedInWorkdir ||
						item.State == FileStatus.NewInIndex ||
						item.State == FileStatus.NewInWorkdir ||
						item.State == FileStatus.RenamedInIndex ||
						item.State == FileStatus.RenamedInWorkdir ||
						isDeleted) // Inclui deletados
					{
						var fullPath = Path.Combine(rootPath, item.FilePath);
						// Adiciona tupla (Caminho, Flag Deletado)
						modifiedFiles.Add((fullPath.Replace("/", "\\"), isDeleted));
					}
				}
			}
			return (IEnumerable<(string, bool)>)modifiedFiles;
		});
	}
}