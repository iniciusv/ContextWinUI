using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Dispatching; // Necessário para capturar o Dispatcher
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class FileExplorerViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;

	// Serviço exposto para a View (MenuFlyout)
	public ITagManagementUiService TagService { get; }

	// Captura o Dispatcher da Thread de UI na criação do ViewModel
	private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> rootItems = new();

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string currentPath = "Nenhum projeto carregado";

	// Eventos
	public event EventHandler<string>? StatusChanged;
	public event EventHandler<FileSystemItem>? FileSelected;

	// Controle de Cancelamento (Debounce)
	private CancellationTokenSource? _searchCts;

	public FileExplorerViewModel(IProjectSessionManager sessionManager, ITagManagementUiService tagService)
	{
		_sessionManager = sessionManager;
		TagService = tagService;

		_sessionManager.ProjectLoaded += OnProjectLoaded;
		_sessionManager.StatusChanged += (s, msg) => OnStatusChanged(msg);
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		RootItems = e.RootItems;
		CurrentPath = e.RootPath;
		IsLoading = false;
		OnStatusChanged("Projeto carregado com sucesso.");
	}

	[RelayCommand]
	private async Task BrowseFolderAsync()
	{
		if (IsLoading) return;

		try
		{
			IsLoading = true;
			OnStatusChanged("Selecionando pasta...");

			await _sessionManager.LoadProjectAsync();
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao abrir projeto: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	public void SelectFile(FileSystemItem item)
	{
		FileSelected?.Invoke(this, item);
	}

	// --- PESQUISA OTIMIZADA (DEBOUNCE + ASYNC RESET) ---
	[RelayCommand]
	private async Task SearchAsync(string query)
	{
		// 1. Cancela busca anterior (se o usuário digitar rápido)
		if (_searchCts != null)
		{
			_searchCts.Cancel();
			_searchCts.Dispose();
		}

		// 2. Cria novo token
		_searchCts = new CancellationTokenSource();
		var token = _searchCts.Token;

		try
		{
			// 3. Debounce de 300ms (espera o usuário parar de digitar)
			await Task.Delay(300, token);

			// 4. Executa a busca (ou limpeza) se não tiver sido cancelado
			if (!token.IsCancellationRequested && RootItems != null)
			{
				// Passamos o DispatcherQueue para permitir manipulação otimizada da UI
				await TreeSearchHelper.SearchAsync(RootItems, query, token, _dispatcherQueue);
			}
		}
		catch (TaskCanceledException)
		{
			// Comportamento esperado do debounce, ignora
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na pesquisa: {ex.Message}");
		}
	}

	// --- COMANDOS DE VISUALIZAÇÃO ---

	[RelayCommand]
	private void ExpandAll()
	{
		if (RootItems == null) return;
		SetExpansionRecursive(RootItems, true);
	}

	[RelayCommand]
	private void CollapseAll()
	{
		if (RootItems == null) return;
		SetExpansionRecursive(RootItems, false);
	}

	[RelayCommand]
	private void ExpandItem(FileSystemItem item)
	{
		if (item != null) item.IsExpanded = true;
	}

	[RelayCommand]
	private void SyncFocus()
	{
		// Recolhe tudo para "focar" (pode ser aprimorado para manter selecionado aberto)
		CollapseAll();
	}

	private void SetExpansionRecursive(IEnumerable<FileSystemItem> items, bool isExpanded)
	{
		foreach (var item in items)
		{
			if (item.IsDirectory)
			{
				item.IsExpanded = isExpanded;
				if (item.Children != null && item.Children.Any())
				{
					SetExpansionRecursive(item.Children, isExpanded);
				}
			}
		}
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}