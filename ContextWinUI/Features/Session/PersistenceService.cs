using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics; // Adicionado para Debug
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ContextWinUI.Features.Session;

public class PersistenceService : IPersistenceService
{
	private const string AppCacheFolderName = "ContextWinUI_Cache";

	public async Task SaveProjectCacheAsync(
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders)
	{
		try
		{
			var cacheFilePath = Path.Combine(projectRootPath, AppCacheFolderName);

			// Filtra apenas arquivos que tem alguma alteração relevante (tags ou ignorado)
			// Isso diminui o tamanho do JSON
			var modifiedStates = states.Where(s => s.IsIgnored || s.Tags.Any());

			var fileDtos = modifiedStates.Select(s => new FileMetadataDto
			{
				RelativePath = Path.GetRelativePath(projectRootPath, s.FullPath),
				IsIgnored = s.IsIgnored,
				Tags = s.Tags.ToList()
			}).ToList();

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
				Files = fileDtos
			};

			var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
			await File.WriteAllTextAsync(cacheFilePath, json);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PersistenceService] Erro ao salvar: {ex.Message}");
			// Não lançamos erro aqui para não travar a aplicação, mas logamos
		}
	}

	public async Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath)
	{
		var cacheFilePath = Path.Combine(projectRootPath, AppCacheFolderName);

		if (!File.Exists(cacheFilePath))
		{
			Debug.WriteLine($"[PersistenceService] Cache não encontrado em: {cacheFilePath}");
			return null;
		}

		try
		{
			var json = await File.ReadAllTextAsync(cacheFilePath);
			var result = JsonSerializer.Deserialize<ProjectCacheDto>(json);
			Debug.WriteLine($"[PersistenceService] Cache carregado com {result?.Files.Count ?? 0} arquivos configurados.");
			return result;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PersistenceService] Erro ao ler JSON: {ex.Message}");
			return null; // Retorna null se o JSON estiver corrompido
		}
	}
}