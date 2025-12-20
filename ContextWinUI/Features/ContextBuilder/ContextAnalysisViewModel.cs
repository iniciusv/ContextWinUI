// ==================== ContextWinUI\Features\ContextBuilder\ContextAnalysisViewModel.cs ====================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Helpers;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels; // Namespace ajustado para padrão

public partial class ContextAnalysisViewModel : ObservableObject
{
	private readonly IRoslynAnalyzerService _roslynAnalyzer;
	private readonly IFileSystemService _fileSystemService;
	private readonly IFileSystemItemFactory _itemFactory;
	private readonly IGitService _gitService;
	private readonly IProjectSessionManager _sessionManager;

	public ITagManagementUiService TagService { get; }
	public ContextSelectionViewModel SelectionVM { get; }

	private readonly Stack<List<FileSystemItem>> _historyStack = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> contextTreeItems = new();

	[ObservableProperty]
	private ObservableCollection<FileSystemItem> gitModifiedItems = new();

	[ObservableProperty]
	private FileSystemItem? selectedItem;

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool canGoBack;

	public int SelectedCount => SelectionVM.SelectedItemsList.Count;

	public event EventHandler<FileSystemItem>? FileSelectedForPreview;
	public event EventHandler<string>? StatusChanged;

	public ContextAnalysisViewModel(
			IRoslynAnalyzerService roslynAnalyzer,
			IFileSystemService fileSystemService,
			IFileSystemItemFactory itemFactory,
			ITagManagementUiService tagService,
			IGitService gitService,
			IProjectSessionManager sessionManager,
			ContextSelectionViewModel selectionVM)
	{
		_roslynAnalyzer = roslynAnalyzer;
		_fileSystemService = fileSystemService;
		_itemFactory = itemFactory;
		TagService = tagService;
		_gitService = gitService;
		_sessionManager = sessionManager;
		SelectionVM = selectionVM;

		SelectionVM.SelectedItemsList.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(SelectedCount));
		};
	}

	public void UpdateSelectionPreview(IEnumerable<FileSystemItem> items)
	{
		if (IsLoading) return;
		ContextTreeItems.Clear();
		SelectionVM.Clear();
		foreach (var item in items)
		{
			var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
			RegisterItemEventsRecursively(fileNode);
			ContextTreeItems.Add(fileNode);
			SelectionVM.AddItem(fileNode);
		}
	}

	public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
	{
		if (!selectedItems.Any()) return;

		IsLoading = true;
		IsVisible = true;

		if (ContextTreeItems.Any())
		{
			_historyStack.Push(ContextTreeItems.ToList());
			UpdateCanGoBack();
		}

		ContextTreeItems.Clear();
		SelectionVM.Clear();
		OnStatusChanged("Analisando estrutura do código...");

		try
		{
			await _roslynAnalyzer.IndexProjectAsync(rootPath);
			foreach (var item in selectedItems)
			{
				var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
				await PopulateNodeAsync(fileNode, item.FullPath);
				RegisterItemEventsRecursively(fileNode);
				ContextTreeItems.Add(fileNode);
				SelectionVM.AddItem(fileNode);
			}
			OnStatusChanged($"Análise concluída.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	private async Task PopulateNodeAsync(FileSystemItem node, string filePath)
	{
		var analysis = await _roslynAnalyzer.AnalyzeFileStructureAsync(filePath);
		if (analysis.Methods.Any())
		{
			var methodsGroup = _itemFactory.CreateWrapper($"{filePath}::methods", FileSystemItemType.LogicalGroup, "\uEA86");
			methodsGroup.SharedState.Name = "Métodos";
			foreach (var method in analysis.Methods)
			{
				var methodItem = _itemFactory.CreateWrapper($"{filePath}::{method}", FileSystemItemType.Method, "\uF158");
				methodItem.SharedState.Name = method;
				methodItem.MethodSignature = method;
				methodsGroup.Children.Add(methodItem);
			}
			node.Children.Add(methodsGroup);
		}
		if (analysis.Dependencies.Any())
		{
			var contextGroup = _itemFactory.CreateWrapper($"{filePath}::deps", FileSystemItemType.LogicalGroup, "\uE71D");
			contextGroup.SharedState.Name = "Dependências";
			foreach (var depPath in analysis.Dependencies)
			{
				var depItem = _itemFactory.CreateWrapper(depPath, FileSystemItemType.Dependency, "\uE943");
				depItem.IsChecked = true;
				contextGroup.Children.Add(depItem);
			}
			node.Children.Add(contextGroup);
		}
		node.IsExpanded = true;
	}

	[RelayCommand]
	public async Task RefreshGitChangesAsync()
	{
		var rootPath = _sessionManager.CurrentProjectPath;

		if (string.IsNullOrEmpty(rootPath) || !_gitService.IsGitRepository(rootPath))
		{
			GitModifiedItems.Clear();
			OnStatusChanged(string.IsNullOrEmpty(rootPath) ? "Nenhum projeto aberto." : "Repositório Git não detectado.");
			return;
		}

		IsLoading = true;
		try
		{
			var changedFiles = await _gitService.GetModifiedFilesAsync(rootPath);
			GitModifiedItems.Clear();

			foreach (var path in changedFiles)
			{
				var item = _itemFactory.CreateWrapper(path, FileSystemItemType.File, "\uE70F");
				RegisterItemEventsRecursively(item);
				GitModifiedItems.Add(item);
			}
			OnStatusChanged("Git atualizado.");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro Git: {ex.Message}");
		}
		finally { IsLoading = false; }
	}

	private void RegisterItemEventsRecursively(FileSystemItem item)
	{
		item.PropertyChanged -= OnItemPropertyChanged;
		item.PropertyChanged += OnItemPropertyChanged;
		if (item.IsChecked) SelectionVM.AddItem(item);
		foreach (var child in item.Children) RegisterItemEventsRecursively(child);
	}

	private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is FileSystemItem item && e.PropertyName == nameof(FileSystemItem.IsChecked))
		{
			if (item.IsChecked) SelectionVM.AddItem(item);
			else SelectionVM.RemoveItem(item);
		}
	}

	// ==================== ContextAnalysisViewModel.cs ====================

	[RelayCommand]
	private async Task AnalyzeItemDepthAsync(FileSystemItem item)
	{
		if (item == null || string.IsNullOrEmpty(item.FullPath)) return;

		// Evita processar pastas, foca em arquivos de código
		if (!item.IsCodeFile) return;

		IsLoading = true;
		try
		{
			// 1. Limpa os filhos atuais (caso já existissem)
			item.Children.Clear();

			// 2. Busca e cria os novos filhos (Métodos e Dependências)
			await PopulateNodeAsync(item, item.FullPath);

			// 3. CORREÇÃO ESSENCIAL: 
			// Registra os eventos nos novos filhos criados. 
			// Sem isso, marcar o checkbox deles não dispara a notificação para a SelectionVM.
			RegisterItemEventsRecursively(item);

			// 4. Expande para mostrar
			item.IsExpanded = true;
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro na análise: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand] private void ExpandAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(true); }
	[RelayCommand] private void CollapseAll() { foreach (var item in ContextTreeItems) item.SetExpansionRecursively(false); }

	[RelayCommand] private void Search(string query) => TreeSearchHelper.Search(ContextTreeItems, query, CancellationToken.None);

	[RelayCommand]
	private void GoBack()
	{
		if (_historyStack.Count > 0)
		{
			var prev = _historyStack.Pop();
			ContextTreeItems.Clear();
			foreach (var item in prev) { ContextTreeItems.Add(item); RegisterItemEventsRecursively(item); }
			UpdateCanGoBack();
		}
	}
	private void UpdateCanGoBack() => CanGoBack = _historyStack.Count > 0;

	public void SelectFileForPreview(FileSystemItem item)
	{
		SelectedItem = item;
		var realPath = GetPhysicalPath(item);

		if (!string.IsNullOrEmpty(realPath) && File.Exists(realPath))
		{
			if (item.Type == FileSystemItemType.Method)
			{
				var tempItem = _itemFactory.CreateWrapper(realPath, FileSystemItemType.File);
				FileSelectedForPreview?.Invoke(this, tempItem);
			}
			else
			{
				FileSelectedForPreview?.Invoke(this, item);
			}
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		ContextTreeItems.Clear();
		SelectionVM.Clear();
		GitModifiedItems.Clear();
		_historyStack.Clear();
		UpdateCanGoBack();
		SelectedItem = null;
	}

	// ==================== ContextAnalysisViewModel.cs ====================

	[RelayCommand]
	public async Task CopyContextToClipboardAsync()
	{
		IsLoading = true;
		try
		{
			// 1. Configurações de limpeza vindas do SessionManager
			bool removeUsings = _sessionManager.OmitUsings;
			bool removeNamespaces = _sessionManager.OmitNamespaces;
			bool removeComments = _sessionManager.OmitComments;
			bool removeEmptyLines = _sessionManager.OmitEmptyLines;

			// 2. Pega a lista plana de itens selecionados (Arquivos, Métodos, Dependências)
			var selectedItems = SelectionVM.SelectedItemsList.ToList();
			if (!selectedItems.Any())
			{
				OnStatusChanged("Nenhum item selecionado.");
				return;
			}

			var sb = new StringBuilder();
			sb.AppendLine("/* CONTEXTO SELECIONADO */");
			sb.AppendLine();

			// 3. Agrupa por caminho físico do arquivo.
			// Isso junta: o nó do arquivo + nós dos métodos + nós de dependências do mesmo arquivo.
			var fileGroups = selectedItems.GroupBy(item => GetPhysicalPath(item)).ToList();

			foreach (var group in fileGroups)
			{
				string filePath = group.Key;

				// Valida se o arquivo existe
				if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) continue;

				sb.AppendLine($"// ==================== {Path.GetFileName(filePath)} ====================");

				// 4. Coleta assinaturas de métodos selecionados
				var selectedMethodSignatures = group
					.Where(item => item.Type == FileSystemItemType.Method)
					.Select(item => item.MethodSignature)
					.Where(s => !string.IsNullOrEmpty(s))
					.Cast<string>()
					.ToList();

				// 5. Verifica se o nó principal (Arquivo) está selecionado
				bool isFileNodeSelected = group.Any(item => item.Type == FileSystemItemType.File);

				// LÓGICA CORRIGIDA: Prioridade para os Métodos
				// Se tivermos QUALQUER método selecionado na lista, assumimos que o usuário
				// quer uma visão filtrada da classe (Estrutura + Métodos Escolhidos).
				// Isso acontece mesmo se 'isFileNodeSelected' for true.
				if (selectedMethodSignatures.Any())
				{
					sb.AppendLine("// (Conteúdo filtrado: Estrutura da classe + Métodos selecionados)");

					// O RoslynAnalyzer vai ler o arquivo e usar o MethodFilterRewriter
					// para manter campos/props e APENAS os métodos desta lista.
					var content = await _roslynAnalyzer.FilterClassContentAsync(
						filePath,
						selectedMethodSignatures,
						removeUsings,
						removeNamespaces,
						removeComments,
						removeEmptyLines
					);
					sb.AppendLine(content);
				}
				// Se NÃO tem métodos selecionados (ex: não expandiu a árvore),
				// mas o arquivo está marcado, copia tudo.
				else if (isFileNodeSelected)
				{
					var rawContent = await _fileSystemService.ReadFileContentAsync(filePath);

					// Limpeza básica (Regex) sem filtrar AST
					var cleanContent = CodeCleanupHelper.ProcessCode(
						rawContent,
						Path.GetExtension(filePath),
						removeUsings,
						removeNamespaces,
						removeComments,
						removeEmptyLines);

					sb.AppendLine(cleanContent);
				}
				else
				{
					// Caso borda: O arquivo não está selecionado, nem métodos, 
					// mas talvez uma dependência solta foi marcada.
					// Nesse caso, decidimos não copiar o código do arquivo pai, 
					// ou copiamos apenas se for estritamente necessário.
					// Aqui, deixo em branco ou você pode adicionar lógica customizada.
				}

				sb.AppendLine();
				sb.AppendLine();
			}

			// 6. Joga para o Clipboard
			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged("Conteúdo copiado com sucesso!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	// Método auxiliar essencial para agrupar corretamente
	private string GetPhysicalPath(FileSystemItem item)
	{
		if (string.IsNullOrEmpty(item.FullPath)) return string.Empty;

		// Se o path for "C:\Proj\File.cs::MetodoA", retorna "C:\Proj\File.cs"
		int separatorIndex = item.FullPath.IndexOf("::");
		if (separatorIndex > 0)
		{
			return item.FullPath.Substring(0, separatorIndex);
		}

		return item.FullPath;
	}


	[RelayCommand] private void AnalyzeMethodFlow(FileSystemItem item) { }
	[RelayCommand] private void SyncFocus() { }
	private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
}