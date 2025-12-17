using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class PersistenceService : IPersistenceService
{
	private const string AppCacheFolderName = "ContextWinUI_Cache";

	public async Task SaveProjectCacheAsync(
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitComments,
		bool includeStructure,
		bool structureOnlyFolders)
	{
		try
		{
			var cacheFilePath = GetCacheFilePath(projectRootPath);
			var cacheDir = Path.GetDirectoryName(cacheFilePath);

			if (cacheDir != null && !Directory.Exists(cacheDir))
			{
				Directory.CreateDirectory(cacheDir);
			}

			var dto = new ProjectCacheDto
			{
				RootPath = projectRootPath,
				PrePrompt = prePrompt,
				OmitUsings = omitUsings,
				OmitComments = omitComments,
				IncludeStructure = includeStructure,
				StructureOnlyFolders = structureOnlyFolders,
				Files = states.Select(s => new FileMetadataDto
				{
					RelativePath = Path.GetRelativePath(projectRootPath, s.FullPath),
					IsIgnored = s.IsIgnored, // Persistindo status
					Tags = s.Tags.ToList()
				}).ToList()
			};

			var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(cacheFilePath, json);
		}
		catch (Exception) { throw; }
	}

	public async Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath)
	{
		try
		{
			var cacheFilePath = GetCacheFilePath(projectRootPath);
			if (!File.Exists(cacheFilePath)) return null;

			var json = await File.ReadAllTextAsync(cacheFilePath);
			return JsonSerializer.Deserialize<ProjectCacheDto>(json);
		}
		catch { return null; }
	}

	private string GetCacheFilePath(string projectPath)
	{
		var appDir = AppDomain.CurrentDomain.BaseDirectory;
		var cacheDir = Path.Combine(appDir, AppCacheFolderName);
		var name = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar));
		var hash = CreateMd5(projectPath);
		return Path.Combine(cacheDir, $"{name}_{hash}.json");
	}

	private string CreateMd5(string input)
	{
		using var md5 = MD5.Create();
		var inputBytes = Encoding.ASCII.GetBytes(input.ToLowerInvariant());
		var hashBytes = md5.ComputeHash(inputBytes);
		return Convert.ToHexString(hashBytes);
	}
}