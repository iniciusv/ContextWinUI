using ContextWinUI.Core.Contracts;
using LibGit2Sharp;
using System;
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
	public Task<string?> GetFileContentFromHeadAsync(string rootPath, string filePath)
	{
		return Task.Run(() =>
		{
			if (!Repository.IsValid(rootPath)) return null;

			try
			{
				using (var repo = new Repository(rootPath))
				{
					var headCommit = repo.Head.Tip;
					if (headCommit == null) return null;

					// 1. Gera o caminho relativo padrão do Windows
					var relativePath = Path.GetRelativePath(rootPath, filePath);

					// 2. Normaliza para o padrão Git (Barras normais /)
					var gitPath = relativePath.Replace("\\", "/");

					// 3. Tenta obter o Entry diretamente
					var treeEntry = headCommit[gitPath];

					// 4. FALLBACK: Se falhar (por casing C:\ vs c:\), procura insensível a caixa
					if (treeEntry == null)
					{
						treeEntry = headCommit.Tree.FirstOrDefault(e =>
							string.Equals(e.Path, gitPath, StringComparison.OrdinalIgnoreCase));

						// Se ainda não achou e o arquivo está em subpastas, o LibGit2Sharp
						// às vezes precisa navegar na árvore. Mas para arquivos na raiz ou 
						// caminhos diretos, o indexador costuma funcionar se o Path estiver exato.
					}

					if (treeEntry == null || treeEntry.TargetType != TreeEntryTargetType.Blob)
					{
						System.Diagnostics.Debug.WriteLine($"[GitService] Arquivo não encontrado no HEAD: {gitPath}");
						return null;
					}

					var blob = (Blob)treeEntry.Target;

					using (var contentStream = blob.GetContentStream())
					using (var reader = new StreamReader(contentStream, System.Text.Encoding.UTF8))
					{
						return reader.ReadToEnd();
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[GitService] Erro ao ler HEAD: {ex.Message}");
				return null;
			}
		});
	}

	public Task<IEnumerable<ContextWinUI.Core.Models.GitCommitItem>> GetCommitHistoryAsync(string rootPath, int limit = 50)
	{
		return Task.Run(() =>
		{
			if (!Repository.IsValid(rootPath))
				return Enumerable.Empty<ContextWinUI.Core.Models.GitCommitItem>();

			var list = new List<ContextWinUI.Core.Models.GitCommitItem>();
			using (var repo = new Repository(rootPath))
			{
				var filter = new CommitFilter { SortBy = CommitSortStrategies.Time };
				var commits = repo.Commits.QueryBy(filter).Take(limit);

				foreach (var c in commits)
				{
					list.Add(new ContextWinUI.Core.Models.GitCommitItem
					{
						Sha = c.Sha,
						Message = c.MessageShort,
						Author = c.Author.Name,
						Date = c.Author.When.DateTime,
						ParentShas = c.Parents.Select(p => p.Sha).ToList()
					});
				}
			}
			return (IEnumerable<ContextWinUI.Core.Models.GitCommitItem>)list;
		});
	}

	public Task<IEnumerable<(string Path, bool IsDeleted)>> GetChangesBetweenCommitsAsync(string rootPath, string oldSha, string newSha)
	{
		return Task.Run(() =>
		{
			var results = new List<(string, bool)>();
			if (!Repository.IsValid(rootPath)) return (IEnumerable<(string, bool)>)results;

			using (var repo = new Repository(rootPath))
			{
				var oldCommit = repo.Lookup<Commit>(oldSha);
				var newCommit = repo.Lookup<Commit>(newSha);

				if (oldCommit == null || newCommit == null) return (IEnumerable<(string, bool)>)results;

				var changes = repo.Diff.Compare<TreeChanges>(oldCommit.Tree, newCommit.Tree);

				foreach (var change in changes)
				{
					// Filter mainly modified/added/deleted/renamed
					string path = change.Path;
					bool isDeleted = change.Status == ChangeKind.Deleted;

					string fullPath = Path.Combine(rootPath, path).Replace("/", "\\");
					results.Add((fullPath, isDeleted));
				}
			}
			return (IEnumerable<(string, bool)>)results;
		});
	}

	public Task<string?> GetFileContentAtCommitAsync(string rootPath, string filePath, string sha)
	{
		return Task.Run(() =>
		{
			if (!Repository.IsValid(rootPath)) return null;

			try
			{
				using (var repo = new Repository(rootPath))
				{
					var commit = repo.Lookup<Commit>(sha);
					if (commit == null) return null;

					var relativePath = Path.GetRelativePath(rootPath, filePath).Replace("\\", "/");
					var treeEntry = commit[relativePath];

					// Fallback case-insensitive
					if (treeEntry == null)
					{
						treeEntry = commit.Tree.FirstOrDefault(e => string.Equals(e.Path, relativePath, StringComparison.OrdinalIgnoreCase));
					}

					if (treeEntry == null || treeEntry.TargetType != TreeEntryTargetType.Blob) return null;

					var blob = (Blob)treeEntry.Target;
					using (var contentStream = blob.GetContentStream())
					using (var reader = new StreamReader(contentStream, System.Text.Encoding.UTF8))
					{
						return reader.ReadToEnd();
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[GitService] Error reading at commit {sha}: {ex.Message}");
				return null;
			}
		});
	}

}