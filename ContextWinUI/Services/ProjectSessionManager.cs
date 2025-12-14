using ContextWinUI.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class ProjectSessionManager : IProjectSessionManager
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IPersistenceService _persistenceService;
	private readonly IFileSystemItemFactory _itemFactory;

	// Estado Global
	public string PrePrompt { get; set; } = string.Empty;
	public bool OmitUsings { get; set; }
	public bool OmitComments { get; set; }
	public bool IncludeStructure { get; set; }
	public bool StructureOnlyFolders { get; set; }

	public string? CurrentProjectPath { get; private set; }
	public bool IsProjectLoaded => !string.IsNullOrEmpty(CurrentProjectPath);

	public event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
	public event EventHandler<string>? StatusChanged;

	public ProjectSessionManager(
		IFileSystemService fileSystemService,
		IPersistenceService persistenceService,
		IFileSystemItemFactory itemFactory)
	{
		_fileSystemService = fileSystemService;
		_persistenceService = persistenceService;
		_itemFactory = itemFactory;
	}

	public async Task OpenProjectAsync(string path)
	{
		if (string.IsNullOrWhiteSpace(path)) return;
		NotifyStatus("Iniciando carregamento do projeto...");
		CloseProject();
		CurrentProjectPath = path;

		try
		{
			NotifyStatus("Lendo arquivos do disco...");
			var rootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);

			NotifyStatus("Verificando cache...");
			var cache = await _persistenceService.LoadProjectCacheAsync(path);

			if (cache != null)
			{
				ApplyCacheToMemory(path, cache);
			}
			else
			{
				PrePrompt = string.Empty;
				OmitUsings = false;
				OmitComments = false;
				IncludeStructure = false;
				StructureOnlyFolders = false;
			}

			NotifyStatus("Projeto carregado.");
			ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(path, rootItems));
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro crítico: {ex.Message}");
			CloseProject();
		}
	}

	public async Task SaveSessionAsync()
	{
		if (!IsProjectLoaded || CurrentProjectPath == null) return;
		NotifyStatus("Salvando sessão...");

		try
		{
			var allStates = _itemFactory.GetAllStates();
			await _persistenceService.SaveProjectCacheAsync(
				CurrentProjectPath,
				allStates,
				PrePrompt,
				OmitUsings,
				OmitComments,
				IncludeStructure,
				StructureOnlyFolders);

			NotifyStatus("Sessão salva com sucesso.");
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro ao salvar: {ex.Message}");
		}
	}

	public void CloseProject()
	{
		CurrentProjectPath = null;
		PrePrompt = string.Empty;
		OmitUsings = false;
		OmitComments = false;
		IncludeStructure = false;
		StructureOnlyFolders = false;
		_itemFactory.ClearCache();
	}

	private void ApplyCacheToMemory(string rootPath, ProjectCacheDto cache)
	{
		PrePrompt = cache.PrePrompt ?? string.Empty;
		OmitUsings = cache.OmitUsings;
		OmitComments = cache.OmitComments;
		IncludeStructure = cache.IncludeStructure;
		StructureOnlyFolders = cache.StructureOnlyFolders;

		foreach (var fileDto in cache.Files)
		{
			var fullPath = Path.Combine(rootPath, fileDto.RelativePath);
			var wrapper = _itemFactory.CreateWrapper(fullPath, FileSystemItemType.File);
			wrapper.SharedState.Tags.Clear();
			foreach (var tag in fileDto.Tags) wrapper.SharedState.Tags.Add(tag);
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);
}