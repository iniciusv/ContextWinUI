using ContextWinUI.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers; // Necessário para FolderPicker

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

	// --- CORREÇÃO: Implementação do Picker ---
	public async Task LoadProjectAsync()
	{
		try
		{
			var folderPicker = new FolderPicker();
			folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
			folderPicker.FileTypeFilter.Add("*");

			// Workaround para obter HWND da janela principal em WinUI 3
			var window = App.MainWindow;
			if (window != null)
			{
				var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
				WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hWnd);
			}

			var folder = await folderPicker.PickSingleFolderAsync();
			if (folder != null)
			{
				await OpenProjectAsync(folder.Path);
			}
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro ao selecionar pasta: {ex.Message}");
		}
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
				// Defaults
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

	// Em ContextWinUI.Services.ProjectSessionManager

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


			bool exists = File.Exists(fullPath) || Directory.Exists(fullPath);

			if (exists)
			{
				var wrapper = _itemFactory.CreateWrapper(fullPath, FileSystemItemType.File);

				wrapper.SharedState.IsIgnored = fileDto.IsIgnored;

				wrapper.SharedState.Tags.Clear();
				foreach (var tag in fileDto.Tags)
				{
					wrapper.SharedState.Tags.Add(tag);
				}
			}
			else
			{
			}
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);
}