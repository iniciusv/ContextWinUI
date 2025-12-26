using ContextWinUI.Models;
using ContextWinUI.Services; // Ajuste namespaces conforme seu projeto
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class PersistenceService : IPersistenceService
{
	private const string AppCacheFolderName = "ContextWinUI_Cache";

	// Implementação do salvamento padrão (calcula hash e redireciona)
	public async Task SaveProjectCacheDefaultAsync(
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders,
		Dictionary<string, string> tagColors)
	{
		var cacheFilePath = GetCacheFilePath(projectRootPath);
		var cacheDir = Path.GetDirectoryName(cacheFilePath);
		if (cacheDir != null && !Directory.Exists(cacheDir))
		{
			Directory.CreateDirectory(cacheDir);
		}

		// Reutiliza a lógica genérica
		await SaveProjectCacheToSpecificFileAsync(cacheFilePath, projectRootPath, states, prePrompt,
			omitUsings, omitNamespaces, omitComments, omitEmptyLines, includeStructure, structureOnlyFolders, tagColors);
	}

	// Implementação Genérica: Salva onde mandarem
	public async Task SaveProjectCacheToSpecificFileAsync(
		string targetFilePath,
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders,
		Dictionary<string, string> tagColors)
	{
		try
		{
			var dto = new ProjectCacheDto
			{
				RootPath = projectRootPath,
				PrePrompt = prePrompt,
				OmitUsings = omitUsings,
				OmitNamespaces = omitNamespaces,
				OmitComments = omitComments,
				OmitEmptyLines = omitEmptyLines,
				IncludeStructure = includeStructure,
				StructureOnlyFolders = structureOnlyFolders,
				TagColors = tagColors ?? new Dictionary<string, string>(),
				Files = states.Select(s => new FileMetadataDto
				{
					RelativePath = Path.GetRelativePath(projectRootPath, s.FullPath),
					IsIgnored = s.IsIgnored,
					Tags = s.Tags.ToList()
				}).ToList()
			};

			var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(targetFilePath, json);
		}
		catch (Exception)
		{
			throw;
		}
	}

	public async Task<ProjectCacheDto?> LoadProjectCacheDefaultAsync(string projectRootPath)
	{
		var cacheFilePath = GetCacheFilePath(projectRootPath);
		return await LoadProjectCacheFromSpecificFileAsync(cacheFilePath);
	}

	public async Task<ProjectCacheDto?> LoadProjectCacheFromSpecificFileAsync(string sourceFilePath)
	{
		try
		{
			if (!File.Exists(sourceFilePath)) return null;
			var json = await File.ReadAllTextAsync(sourceFilePath);
			return JsonSerializer.Deserialize<ProjectCacheDto>(json);
		}
		catch
		{
			return null;
		}
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
		var inputBytes = Encoding.ASCII.GetBytes(input.ToLowerInvariant());
		var hashBytes = MD5.HashData(inputBytes); // Sintaxe .NET mais nova, ou use MD5.Create()
		return Convert.ToHexString(hashBytes);
	}
}