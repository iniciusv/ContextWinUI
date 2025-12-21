using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.Features.ContextBuilder
{
	public partial class ContextAnalysisViewModel : ObservableObject
	{
		private readonly IRoslynAnalyzerService _roslynAnalyzer;
		private readonly IFileSystemItemFactory _itemFactory;
		private readonly IDependencyAnalysisOrchestrator _analysisOrchestrator;
		private readonly IProjectSessionManager _sessionManager;

		// Sub-ViewModels
		public ContextTreeViewModel TreeVM { get; }
		public ContextGitViewModel GitVM { get; }
		public ContextSelectionViewModel SelectionVM { get; }
		public ITagManagementUiService TagService { get; }

		[ObservableProperty] private bool isVisible;
		[ObservableProperty] private bool isLoading;
		[ObservableProperty] private bool canGoBack;
		[ObservableProperty] private FileSystemItem? selectedPreviewItem;

		public int SelectedCount => SelectionVM.SelectedItemsList.Count;

		public event EventHandler<FileSystemItem>? FileSelectedForPreview;
		public event EventHandler<string>? StatusChanged;

		private readonly Stack<List<FileSystemItem>> _historyStack = new();

		public ContextAnalysisViewModel(
			IRoslynAnalyzerService roslynAnalyzer,
			IFileSystemItemFactory itemFactory,
			IDependencyAnalysisOrchestrator analysisOrchestrator,
			IProjectSessionManager sessionManager,
			IGitService gitService,
			ITagManagementUiService tagService,
			ContextSelectionViewModel selectionVM)
		{
			_roslynAnalyzer = roslynAnalyzer;
			_itemFactory = itemFactory;
			_analysisOrchestrator = analysisOrchestrator;
			_sessionManager = sessionManager;
			TagService = tagService;
			SelectionVM = selectionVM;

			TreeVM = new ContextTreeViewModel(itemFactory, analysisOrchestrator, sessionManager);
			GitVM = new ContextGitViewModel(gitService, itemFactory, sessionManager);

			// Sincronização de eventos
			TreeVM.Items.CollectionChanged += (s, e) => RegisterCollectionEvents(e.NewItems);
			GitVM.ModifiedItems.CollectionChanged += (s, e) => RegisterCollectionEvents(e.NewItems);
			SelectionVM.SelectedItemsList.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SelectedCount));

			TreeVM.StructureUpdated += (s, parentItem) => RegisterItemRecursively(parentItem);
		}

		public async Task AnalyzeContextAsync(List<FileSystemItem> selectedItems, string rootPath)
		{
			IsLoading = true;
			IsVisible = true;

			if (TreeVM.Items.Any())
			{
				_historyStack.Push(TreeVM.Items.ToList());
				CanGoBack = true;
			}

			TreeVM.Clear();
			GitVM.Clear();
			SelectionVM.Clear();
			OnStatusChanged("Inicializando análise...");

			try
			{
				await _roslynAnalyzer.IndexProjectAsync(rootPath);

				var treeNodes = new List<FileSystemItem>();
				foreach (var item in selectedItems)
				{
					var fileNode = _itemFactory.CreateWrapper(item.FullPath, FileSystemItemType.File, "\uE943");
					await _analysisOrchestrator.EnrichFileNodeAsync(fileNode, rootPath);
					treeNodes.Add(fileNode);
				}

				TreeVM.SetItems(treeNodes);

				foreach (var node in treeNodes) RegisterItemRecursively(node);

				_ = GitVM.RefreshChangesAsync();

				OnStatusChanged("Análise concluída.");
			}
			catch (Exception ex)
			{
				OnStatusChanged($"Erro: {ex.Message}");
			}
			finally { IsLoading = false; }
		}

		public void UpdateSelectionPreview(IEnumerable<FileSystemItem> items)
		{
			if (IsLoading) return;
			TreeVM.SetItems(items);
			foreach (var item in items) RegisterItemRecursively(item);
		}

		private void RegisterCollectionEvents(System.Collections.IList? newItems)
		{
			if (newItems == null) return;
			foreach (FileSystemItem item in newItems) RegisterItemRecursively(item);
		}

		private void RegisterItemRecursively(FileSystemItem item)
		{
			item.PropertyChanged -= OnItemPropertyChanged;
			item.PropertyChanged += OnItemPropertyChanged;

			if (item.IsChecked) SelectionVM.AddItem(item);
			foreach (var child in item.Children) RegisterItemRecursively(child);
		}

		private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FileSystemItem.IsChecked) && sender is FileSystemItem item)
			{
				if (item.IsChecked) SelectionVM.AddItem(item);
				else SelectionVM.RemoveItem(item);
			}
		}

		public void SelectFileForPreview(FileSystemItem item)
		{
			SelectedPreviewItem = item;
			string realPath = item.FullPath;
			if (item.FullPath.Contains("::"))
				realPath = item.FullPath.Substring(0, item.FullPath.IndexOf("::"));

			if (!string.IsNullOrEmpty(realPath) && System.IO.File.Exists(realPath))
			{
				if (item.Type != FileSystemItemType.File)
				{
					var tempItem = _itemFactory.CreateWrapper(realPath, FileSystemItemType.File);
					FileSelectedForPreview?.Invoke(this, tempItem);
				}
				else FileSelectedForPreview?.Invoke(this, item);
			}
		}

		[RelayCommand]
		public async Task CopyContextToClipboardAsync()
		{
			var items = SelectionVM.SelectedItemsList.ToList();
			if (!items.Any()) return;
			IsLoading = true;
			try
			{
				string text = await _analysisOrchestrator.BuildContextStringAsync(items, _sessionManager);
				var dp = new DataPackage();
				dp.SetText(text);
				Clipboard.SetContent(dp);
				OnStatusChanged("Copiado com sucesso!");
			}
			finally { IsLoading = false; }
		}

		[RelayCommand]
		private void GoBack()
		{
			if (_historyStack.Count > 0)
			{
				var prev = _historyStack.Pop();
				TreeVM.SetItems(prev);
				foreach (var item in prev) RegisterItemRecursively(item);
				CanGoBack = _historyStack.Count > 0;
			}
		}

		[RelayCommand]
		private void Close()
		{
			IsVisible = false;
			TreeVM.Clear();
			GitVM.Clear();
			SelectionVM.Clear();
			_historyStack.Clear();
			CanGoBack = false;
			SelectedPreviewItem = null;
		}

		private void OnStatusChanged(string message) => StatusChanged?.Invoke(this, message);
	}
}