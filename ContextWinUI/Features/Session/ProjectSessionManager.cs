using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace ContextWinUI.Services;

public class ProjectSessionManager : IProjectSessionManager
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IPersistenceService _persistenceService;
	private readonly IFileSystemItemFactory _itemFactory;

	public string PrePrompt { get; set; } = string.Empty;

	public bool OmitUsings { get; set; }
	public bool OmitNamespaces { get; set; }
	public bool OmitComments { get; set; }
	public bool OmitEmptyLines { get; set; }

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

	public async Task LoadProjectAsync()
	{
		try
		{
			var folderPicker = new FolderPicker();
			folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
			folderPicker.FileTypeFilter.Add("*");

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
				PrePrompt = string.Empty;
				OmitUsings = false;
				OmitNamespaces = false;
				OmitComments = false;
				OmitEmptyLines = false;
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
				OmitNamespaces,
				OmitComments,
				OmitEmptyLines,
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
		OmitNamespaces = false;
		OmitComments = false;
		OmitEmptyLines = false;
		IncludeStructure = false;
		StructureOnlyFolders = false;
		_itemFactory.ClearCache();
	}

	private void ApplyCacheToMemory(string rootPath, ProjectCacheDto cache)
	{
		// 1. Aplica configurações globais
		PrePrompt = cache.PrePrompt ?? string.Empty;
		OmitUsings = cache.OmitUsings;
		OmitNamespaces = cache.OmitNamespaces;
		OmitComments = cache.OmitComments;
		OmitEmptyLines = cache.OmitEmptyLines;
		IncludeStructure = cache.IncludeStructure;
		StructureOnlyFolders = cache.StructureOnlyFolders;

		// 2. Aplica Tags e Ignorados aos Arquivos
		foreach (var fileDto in cache.Files)
		{
			try
			{
				// Normalização robusta de caminho
				var fullPath = Path.GetFullPath(Path.Combine(rootPath, fileDto.RelativePath));

				// Verifica se o arquivo ainda existe no disco
				if (File.Exists(fullPath) || Directory.Exists(fullPath))
				{
					// O Factory usa cache interno. Ao chamar CreateWrapper com o mesmo caminho,
					// ele retorna a MESMA instância que está sendo exibida no TreeView.
					// Isso é vital para que as tags apareçam na UI.
					var wrapper = _itemFactory.CreateWrapper(fullPath, FileSystemItemType.File);

					// Atualiza o estado compartilhado (SharedState)
					wrapper.SharedState.IsIgnored = fileDto.IsIgnored;

					// Atualiza a coleção de tags (ObservableCollection notifica a UI automaticamente)
					wrapper.SharedState.Tags.Clear();
					foreach (var tag in fileDto.Tags)
					{
						wrapper.SharedState.Tags.Add(tag);
					}
				}
			}
			catch
			{
				// Ignora falhas em arquivos individuais (ex: caracteres inválidos no cache antigo)
				continue;
			}
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);
}