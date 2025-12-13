using ContextWinUI.ContextWinUI.Models;
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

	// Estado Global da Sessão
	public string PrePrompt { get; set; } = string.Empty;
	public bool OmitUsings { get; set; }
	public bool OmitComments { get; set; }

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
			// 1. Carrega Arquivos
			NotifyStatus("Lendo arquivos do disco...");
			var rootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);

			// 2. Carrega Cache
			NotifyStatus("Verificando cache de metadados...");
			var cache = await _persistenceService.LoadProjectCacheAsync(path);

			if (cache != null)
			{
				NotifyStatus("Aplicando histórico e contexto...");
				ApplyCacheToMemory(path, cache);
			}
			else
			{
				// Padrões se não houver cache
				PrePrompt = string.Empty;
				OmitUsings = false;
				OmitComments = false;
			}

			NotifyStatus("Projeto carregado com sucesso.");
			ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(path, rootItems));
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro crítico ao abrir projeto: {ex.Message}");
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

			// Salva passando todas as configs atuais
			await _persistenceService.SaveProjectCacheAsync(
				CurrentProjectPath,
				allStates,
				PrePrompt,
				OmitUsings,
				OmitComments);

			NotifyStatus("Sessão salva com sucesso (local).");
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro ao salvar sessão: {ex.Message}");
		}
	}

	public void CloseProject()
	{
		CurrentProjectPath = null;
		PrePrompt = string.Empty;
		OmitUsings = false;
		OmitComments = false;
		_itemFactory.ClearCache();
	}

	private void ApplyCacheToMemory(string rootPath, ProjectCacheDto cache)
	{
		// Restaura configurações
		PrePrompt = cache.PrePrompt ?? string.Empty;
		OmitUsings = cache.OmitUsings;
		OmitComments = cache.OmitComments;

		// Restaura Tags
		foreach (var fileDto in cache.Files)
		{
			var fullPath = Path.Combine(rootPath, fileDto.RelativePath);
			var wrapper = _itemFactory.CreateWrapper(fullPath, FileSystemItemType.File);

			wrapper.SharedState.Tags.Clear();
			foreach (var tag in fileDto.Tags)
			{
				wrapper.SharedState.Tags.Add(tag);
			}
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);
}