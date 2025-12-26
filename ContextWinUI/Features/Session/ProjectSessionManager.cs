using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.UI;

namespace ContextWinUI.Services;

public class ProjectSessionManager : IProjectSessionManager
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IPersistenceService _persistenceService;
	private readonly IFileSystemItemFactory _itemFactory;

	// ESTADO: Caminho do arquivo de contexto atualmente em uso (se o usuário abriu um manualmente)
	public string? ActiveContextFilePath { get; private set; }

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
	public ConcurrentDictionary<string, string> TagColors { get; } = new();

	public ProjectSessionManager(IFileSystemService fileSystemService, IPersistenceService persistenceService, IFileSystemItemFactory itemFactory)
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
		CloseProject(); // Limpa tudo

		// IMPORTANTE: Ao abrir uma nova pasta, resetamos o arquivo de contexto para o padrão (null)
		ActiveContextFilePath = null;

		CurrentProjectPath = path;

		try
		{
			NotifyStatus("Lendo arquivos do disco...");
			var rootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);

			NotifyStatus("Verificando cache padrão...");
			// Carrega do cache padrão inicialmente
			var cache = await _persistenceService.LoadProjectCacheDefaultAsync(path);

			TagColors.Clear();
			if (cache != null)
			{
				ApplyCacheToMemory(path, cache);
				ApplyColorsFromCache(cache);
			}
			else
			{
				ResetSettingsToDefault();
			}

			NotifyStatus("Projeto carregado (Modo Padrão).");
			ProjectLoaded?.Invoke(this, new ProjectLoadedEventArgs(path, rootItems));
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro crítico: {ex.Message}");
			CloseProject();
		}
	}

	public void CloseProject()
	{
		CurrentProjectPath = null;
		ActiveContextFilePath = null; // Reseta o caminho ativo
		ResetSettingsToDefault();
		_itemFactory.ClearCache();
	}

	private void ResetSettingsToDefault()
	{
		PrePrompt = string.Empty;
		OmitUsings = false;
		OmitNamespaces = false;
		OmitComments = false;
		OmitEmptyLines = false;
		IncludeStructure = false;
		StructureOnlyFolders = false;
	}

	private void ApplyColorsFromCache(ProjectCacheDto cache)
	{
		foreach (var kvp in cache.TagColors)
		{
			TagColors.TryAdd(kvp.Key, kvp.Value);
			try
			{
				var color = ParseColorHex(kvp.Value);
				TagColorService.Instance.SetColorForTag(kvp.Key, color);
			}
			catch { }
		}
	}

	private void ApplyCacheToMemory(string rootPath, ProjectCacheDto cache)
	{
		PrePrompt = cache.PrePrompt ?? string.Empty;
		OmitUsings = cache.OmitUsings;
		OmitNamespaces = cache.OmitNamespaces;
		OmitComments = cache.OmitComments;
		OmitEmptyLines = cache.OmitEmptyLines;
		IncludeStructure = cache.IncludeStructure;
		StructureOnlyFolders = cache.StructureOnlyFolders;

		// Importante: Limpar tags antigas antes de aplicar as novas do arquivo
		var allItems = _itemFactory.GetAllStates();
		foreach (var item in allItems)
		{
			// Opcional: Você pode querer limpar tudo ou fazer merge. 
			// Para "usar o arquivo de contexto", geralmente limpamos o estado atual primeiro ou sobrescrevemos.
			// Aqui estamos assumindo sobrescrever apenas os que estão no arquivo:
		}

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
		}
	}
	private string ToHex(Color c)
	{
		return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
	}

	private Color ParseColorHex(string hex)
	{
		if (string.IsNullOrEmpty(hex)) return Color.FromArgb(255, 0, 120, 215); // Cor padrão se nulo

		try
		{
			hex = hex.Replace("#", "");
			byte a = 255;
			byte r = 0;
			byte g = 0;
			byte b = 0;

			// Formato #AARRGGBB
			if (hex.Length == 8)
			{
				a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
				r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
				g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
				b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
			}
			// Formato #RRGGBB
			else if (hex.Length == 6)
			{
				r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
				g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
				b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
			}

			return Color.FromArgb(a, r, g, b);
		}
		catch
		{
			// Retorna azul padrão em caso de erro no parse
			return Color.FromArgb(255, 0, 120, 215);
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);

	// NOVO MÉTODO: Carrega um arquivo de contexto e o define como ATIVO
	public async Task LoadContextFromFileAsync(string filePath)
	{
		if (!IsProjectLoaded || CurrentProjectPath == null) return;

		NotifyStatus($"Lendo arquivo de contexto: {Path.GetFileName(filePath)}...");

		var cache = await _persistenceService.LoadProjectCacheFromSpecificFileAsync(filePath);
		if (cache != null)
		{
			// 1. Aplica os dados na memória
			ApplyCacheToMemory(CurrentProjectPath, cache);

			// 2. Aplica as cores
			if (cache.TagColors != null)
			{
				TagColors.Clear();
				ApplyColorsFromCache(cache);
			}

			// 3. Define este arquivo como a fonte da verdade para o próximo "Salvar"
			ActiveContextFilePath = filePath;

			NotifyStatus($"Contexto carregado e vinculado: {Path.GetFileName(filePath)}");
		}
		else
		{
			NotifyStatus("Falha ao ler ou interpretar o arquivo de contexto.");
		}
	}

	// MÉTODO MODIFICADO: Salva no arquivo ativo (se existir) ou no padrão
	public async Task SaveSessionAsync()
	{
		if (!IsProjectLoaded || CurrentProjectPath == null) return;

		try
		{
			var allStates = _itemFactory.GetAllStates();
			var currentColors = TagColorService.Instance.GetAllColors();
			var colorsToSave = new Dictionary<string, string>();
			foreach (var kvp in currentColors) colorsToSave[kvp.Key] = ToHex(kvp.Value);

			// Atualiza dicionário local
			TagColors.Clear();
			foreach (var c in colorsToSave) TagColors.TryAdd(c.Key, c.Value);

			if (!string.IsNullOrEmpty(ActiveContextFilePath))
			{
				// SALVA NO ARQUIVO ESCOLHIDO PELO USUÁRIO
				NotifyStatus($"Salvando em: {Path.GetFileName(ActiveContextFilePath)}...");
				await _persistenceService.SaveProjectCacheToSpecificFileAsync(
					ActiveContextFilePath,
					CurrentProjectPath,
					allStates, PrePrompt, OmitUsings, OmitNamespaces, OmitComments, OmitEmptyLines, IncludeStructure, StructureOnlyFolders, colorsToSave);
			}
			else
			{
				// SALVA NO CACHE PADRÃO (AppData)
				NotifyStatus("Salvando sessão no cache padrão...");
				await _persistenceService.SaveProjectCacheDefaultAsync(
					CurrentProjectPath,
					allStates, PrePrompt, OmitUsings, OmitNamespaces, OmitComments, OmitEmptyLines, IncludeStructure, StructureOnlyFolders, colorsToSave);
			}

			NotifyStatus("Trabalho salvo com sucesso.");
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro ao salvar: {ex.Message}");
		}
	}

	// Método auxiliar para exportar (Salvar Como) - não altera o ActiveContextFilePath a menos que desejado
	public async Task ExportContextAsAsync(string filePath)
	{
		if (!IsProjectLoaded || CurrentProjectPath == null) return;

		var allStates = _itemFactory.GetAllStates();
		var currentColors = TagColorService.Instance.GetAllColors();
		var colorsToSave = new Dictionary<string, string>();
		foreach (var kvp in currentColors) colorsToSave[kvp.Key] = ToHex(kvp.Value);

		await _persistenceService.SaveProjectCacheToSpecificFileAsync(
			filePath,
			CurrentProjectPath,
			allStates, PrePrompt, OmitUsings, OmitNamespaces, OmitComments, OmitEmptyLines, IncludeStructure, StructureOnlyFolders, colorsToSave);

		// Opcional: Se "Exportar" deve virar o arquivo ativo, descomente a linha abaixo:
		// ActiveContextFilePath = filePath; 

		NotifyStatus($"Cópia exportada para: {Path.GetFileName(filePath)}");
	}
}