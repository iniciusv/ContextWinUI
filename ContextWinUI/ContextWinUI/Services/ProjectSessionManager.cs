using ContextWinUI.ContextWinUI.Models;
using ContextWinUI.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class ProjectSessionManager : IProjectSessionManager
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IPersistenceService _persistenceService;
	private readonly IFileSystemItemFactory _itemFactory;

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
		CloseProject(); // Garante limpeza anterior

		CurrentProjectPath = path;

		try
		{
			// 1. Carrega estrutura física (Disco)
			// A Factory já vai criando os Wrappers/States
			NotifyStatus("Lendo arquivos do disco...");
			var rootItems = await _fileSystemService.LoadProjectRecursivelyAsync(path);

			// 2. Carrega Metadados (Cache JSON)
			NotifyStatus("Verificando cache de metadados...");
			var cache = await _persistenceService.LoadProjectCacheAsync(path);

			if (cache != null)
			{
				NotifyStatus("Aplicando tags e histórico...");
				ApplyCacheToMemory(path, cache);
			}

			// 3. Notifica todo o sistema que o projeto está pronto
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
			// Pega todos os estados da memória via Factory
			var allStates = _itemFactory.GetAllStates(); // Método que criamos no passo anterior

			await _persistenceService.SaveProjectCacheAsync(CurrentProjectPath, allStates);

			NotifyStatus("Sessão salva.");
		}
		catch (Exception ex)
		{
			NotifyStatus($"Erro ao salvar sessão: {ex.Message}");
		}
	}

	public void CloseProject()
	{
		CurrentProjectPath = null;
		_itemFactory.ClearCache(); // Limpa a memória dos Flyweights
								   // Poderíamos disparar um evento ProjectClosed aqui se necessário
	}

	private void ApplyCacheToMemory(string rootPath, ProjectCacheDto cache)
	{
		foreach (var fileDto in cache.Files)
		{
			var fullPath = Path.Combine(rootPath, fileDto.RelativePath);

			// A Factory é inteligente:
			// Como acabamos de carregar o LoadProjectRecursivelyAsync, o estado JÁ EXISTE no dicionário.
			// O CreateWrapper vai apenas recuperar esse estado existente.
			var wrapper = _itemFactory.CreateWrapper(fullPath, FileSystemItemType.File);

			// Atualiza o estado compartilhado
			wrapper.SharedState.Tags.Clear();
			foreach (var tag in fileDto.Tags)
			{
				wrapper.SharedState.Tags.Add(tag);
			}

			// Opcional: Se salvou estado de seleção
			// wrapper.IsChecked = true; 
		}
	}

	private void NotifyStatus(string msg) => StatusChanged?.Invoke(this, msg);
}