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
		CloseProject();
		CurrentProjectPath = path;

		try
		{
			NotifyStatus("Lendo arquivos do disco...");
			var rootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);

			NotifyStatus("Verificando cache...");
			var cache = await _persistenceService.LoadProjectCacheAsync(path);

			// Limpa dicionário local (opcional, já que usamos o TagColorService)
			TagColors.Clear();

			if (cache != null)
			{
				ApplyCacheToMemory(path, cache);

				// CORREÇÃO: Carregar cores do cache para o TagColorService
				if (cache.TagColors != null)
				{
					foreach (var kvp in cache.TagColors)
					{
						// Atualiza propriedade local
						TagColors.TryAdd(kvp.Key, kvp.Value);

						// Atualiza o Singleton que a UI usa
						try
						{
							var color = ParseColorHex(kvp.Value);
							TagColorService.Instance.SetColorForTag(kvp.Key, color);
						}
						catch { /* Ignora cores mal formatadas */ }
					}
				}
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

			// CORREÇÃO: Pegar as cores atuais do TagColorService e converter para Hex
			var currentColors = TagColorService.Instance.GetAllColors();
			var colorsToSave = new Dictionary<string, string>();

			foreach (var kvp in currentColors)
			{
				colorsToSave[kvp.Key] = ToHex(kvp.Value);
			}

			// Atualiza também a propriedade local TagColors para manter sincronia
			TagColors.Clear();
			foreach (var c in colorsToSave) TagColors.TryAdd(c.Key, c.Value);

			await _persistenceService.SaveProjectCacheAsync(
				CurrentProjectPath,
				allStates,
				PrePrompt,
				OmitUsings,
				OmitNamespaces,
				OmitComments,
				OmitEmptyLines,
				IncludeStructure,
				StructureOnlyFolders,
				colorsToSave); // Passa o dicionário atualizado

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
		PrePrompt = cache.PrePrompt ?? string.Empty;
		OmitUsings = cache.OmitUsings;
		OmitNamespaces = cache.OmitNamespaces;
		OmitComments = cache.OmitComments;
		OmitEmptyLines = cache.OmitEmptyLines;
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
}