using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class FileSegmentsViewModel : ObservableObject, IFileSegmentsContract
{
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;
	private readonly ISymbolResolutionService _symbolService;
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _mergerService;
	private readonly IBlockEditorService _editorService;
	private readonly IBlockLoaderService _loaderService;
    private readonly IBlockSelectionManager _selectionManager; // NEW

	public event EventHandler? FileSelectionRequested;


	public string FilePath { get; }
	public string FileName { get; }

	[ObservableProperty]
	private ObservableCollection<SegmentRowViewModel> rows = new();

	public IEnumerable<CodeBlockItem> Blocks => Rows.Select(r => r.Current);

	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
	private CodeBlockItem? selectedBlock;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isEmpty;

	[ObservableProperty]
	private string currentSymbolInfo = string.Empty;

	[ObservableProperty]
	private GridLength leftColumnWidth = new GridLength(0);

	[ObservableProperty]
	private bool isComparisonMode;

	[ObservableProperty]
	private bool hideUnchangedBlocks;

	[ObservableProperty]
	private int compareLeftIndex = 0;

	[ObservableProperty]
	private int currentGlobalIndex = 0;

	[ObservableProperty]
	private bool isPreview;

	public bool HasSelectedBlock => SelectedBlock != null;
	public bool HasAnyUnsavedChanges => Rows.Any(r => r.Current.HasUnsavedChanges);
	public bool HasAnyBlockSelected => Rows.Any(r => r.Current.IsSelected);

	public event EventHandler? GlobalSaveRequested;
	public event EventHandler<int>? GlobalRestoreRequested;

	public FileSegmentsViewModel(
		string filePath,
		IFileSystemService fileSystemService,
		ICodeBlockParserService parserService,
		ISymbolResolutionService symbolService,
		IVersionDiffManager diffManager,
		ObservableCollection<GlobalVersion> sharedHistory,
		int initialGlobalIndex,
		IAiCodeMerger mergerService,
		IBlockLoaderService loaderService,
		IBlockEditorService editorService,
        IBlockSelectionManager selectionManager) // NEW
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_symbolService = symbolService;
		_diffManager = diffManager;
		GlobalHistory = sharedHistory;
		CurrentGlobalIndex = initialGlobalIndex;
		_mergerService = mergerService;
		_editorService = editorService;
		_loaderService = loaderService;
        _selectionManager = selectionManager;

		_ = LoadBlocksAsync();
	}

	partial void OnIsComparisonModeChanged(bool value)
	{
		LeftColumnWidth = value ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
		if (value)
		{
			_diffManager.RefreshReferenceColumns(Rows, GlobalHistory, CompareLeftIndex);
			_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
		}
		else
		{
			foreach (var row in Rows) row.IsVisible = true;
		}
	}

	partial void OnCompareLeftIndexChanged(int value)
	{
		if (IsComparisonMode)
		{
			_diffManager.RefreshReferenceColumns(Rows, GlobalHistory, value);
			if (HideUnchangedBlocks)
				_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
		}
	}

	partial void OnHideUnchangedBlocksChanged(bool value)
	{
		_diffManager.UpdateRowsVisibility(Rows, value);
	}


	// Método LoadBlocksAsync completo modificado
	private async Task LoadBlocksAsync()
	{
		IsLoading = true;
		SelectedBlock = null;
		try
		{
			var items = await _loaderService.LoadAndProcessFileAsync(FilePath);
			var bufferList = new List<SegmentRowViewModel>(items.Count);

			foreach (var item in items)
			{
				if (item.HasUnsavedChanges)
				{
					item.InitializeVersions(item.Content);
				}

                // Initialize selection state from manager
                if (_selectionManager.IsBlockSelected(FilePath, item.StableId))
                {
                    item.IsSelected = true;
                }

				// --- ALTERAÇÃO AQUI: Listener para sincronizar seleção ---
				AttachBlockEvents(item);
				// ---------------------------------------------------------

				bufferList.Add(new SegmentRowViewModel(item));
			}

			Rows = new ObservableCollection<SegmentRowViewModel>(bufferList);

			if (Rows.Any())
			{
				SelectedBlock = Rows.First().Current;
			}

			RestoreToGlobalIndex(CurrentGlobalIndex);
			NotifyUnsavedChanges();

			if (IsComparisonMode)
			{
				_diffManager.RefreshReferenceColumns(Rows, GlobalHistory, CompareLeftIndex);
				_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
			}
		}
		catch (Exception ex)
		{
			Rows = new ObservableCollection<SegmentRowViewModel>
		{
			new SegmentRowViewModel(new CodeBlockItem
			{
				Name = "Erro",
				Content = ex.Message,
				SegmentType = SegmentType.Trivia
			})
		};
		}
		finally
		{
			IsLoading = false;
			IsEmpty = Rows.Count == 0;
		}
	}
	public string GetExportContent()
	{
		// Regra 1: Se houver blocos específicos selecionados, exporta SÓ ELES.
		var selectedRows = Rows.Where(r => r.Current.IsSelected).ToList();

		if (selectedRows.Any())
		{
			var sb = new StringBuilder();
			sb.AppendLine($"// File: {FileName} (Partial Selection)");
			foreach (var row in selectedRows)
			{
				sb.AppendLine(row.Current.Content);
			}
			return sb.ToString();
		}

		// Regra 2: Se NENHUM bloco estiver selecionado, exporta o ARQUIVO TODO.
		var fullSb = new StringBuilder();
		fullSb.AppendLine($"// File: {FileName} (Full Content)");
		foreach (var row in Rows)
		{
			fullSb.Append(row.Current.Content);
		}
		return fullSb.ToString();
	}
	// -----------------------------------------

	[RelayCommand]
	public void AddSiblingBlock(CodeBlockItem? referenceBlock)
	{
		if (referenceBlock == null) return;
		var newBlock = _editorService.AddSiblingBlock(Rows, referenceBlock);
		if (newBlock != null)
		{
			SelectedBlock = newBlock;
			NotifyUnsavedChanges();
			RefreshDiffVisibility();
		}
	}

	[RelayCommand]
	public void DeleteBlock(CodeBlockItem? block)
	{
		if (block == null) return;
		UpdateSelectionBeforeDelete(block);
		_editorService.DeleteBlock(Rows, block);
		NotifyUnsavedChanges();
		RefreshDiffVisibility();
	}

	[RelayCommand]
	public void LinkBlocks(Tuple<CodeBlockItem, CodeBlockItem> items)
	{
		_editorService.LinkBlocks(Rows, items.Item1, items.Item2);
		NotifyUnsavedChanges();
		RefreshDiffVisibility();
	}

	[RelayCommand]
	public void RevertBlockToOriginal(CodeBlockItem? currentBlock)
	{
		if (currentBlock == null) return;
		bool changed = _editorService.RevertBlockToOriginal(currentBlock);
		if (changed)
		{
			NotifyUnsavedChanges();
			RefreshDiffVisibility();
		}
	}

	[RelayCommand]
	public void PromoteEditToNewBlock(CodeBlockItem? currentBlock)
	{
		_editorService.PromoteEditToNewBlock(Rows, currentBlock);
		NotifyUnsavedChanges();
		RefreshDiffVisibility();
	}

	private void RefreshDiffVisibility()
	{
		if (IsComparisonMode)
			_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
	}

	private void UpdateSelectionBeforeDelete(CodeBlockItem blockToDelete)
	{
		if (SelectedBlock == blockToDelete)
		{
			var row = Rows.FirstOrDefault(r => r.Current == blockToDelete);
			if (row == null) return;
			int idx = Rows.IndexOf(row);
			if (idx > 0) SelectedBlock = Rows[idx - 1].Current;
			else if (Rows.Count > 1) SelectedBlock = Rows[idx + 1].Current;
			else SelectedBlock = null;
		}
	}

	[RelayCommand]
	public void SaveChanges(string mode)
	{
		if (mode == "NewVersion")
		{
			TriggerGlobalSave();
		}
		else if (mode == "Overwrite")
		{
			foreach (var row in Rows)
			{
				var block = row.Current;
				if (block.HasUnsavedChanges && block.Versions.Any())
				{
					var currentVer = block.Versions[block.CurrentVersionIndex];
					currentVer.Content = block.Content;
					currentVer.Timestamp = DateTime.Now;
					block.RestoreVersion(block.CurrentVersionIndex);
				}
			}
			NotifyUnsavedChanges();
		}
	}

	[RelayCommand]
	public void CommitAllPendingChanges() => TriggerGlobalSave();

	public void RestoreToGlobalIndex(int globalIndex)
	{
		if (GlobalHistory == null || globalIndex < 0 || globalIndex >= GlobalHistory.Count) return;
		CurrentGlobalIndex = globalIndex;
		_diffManager.RestoreBlocksToGlobalVersion(Rows, GlobalHistory[globalIndex]);
		OnPropertyChanged(nameof(HasAnyUnsavedChanges));
		if (IsComparisonMode) _diffManager.RefreshReferenceColumns(Rows, GlobalHistory, CompareLeftIndex);
	}

	public void SnapshotBlocksForGlobalVersion(string description, DateTime batchTimestamp)
	{
		foreach (var row in Rows)
		{
			var block = row.Current;
			if (block.HasUnsavedChanges)
			{
				block.CreateNewVersion(block.Content, description, batchTimestamp);
			}
		}
		OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	}

	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	public void SyncGlobalIndex(int index) => SetProperty(ref currentGlobalIndex, index, nameof(CurrentGlobalIndex));
	partial void OnCurrentGlobalIndexChanged(int value) => GlobalRestoreRequested?.Invoke(this, value);

	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);

	public void ResolveSymbolHeuristic(int cursorIndexInBlock)
	{
		if (SelectedBlock == null) return;
		string text = SelectedBlock.Content;

		if (cursorIndexInBlock > text.Length)
			cursorIndexInBlock = text.Length;

		var result = _symbolService.ResolveSymbolAtPosition(
			text,
			cursorIndexInBlock,
			SelectedBlock.AbsoluteStartPosition,
			FilePath
		);
		CurrentSymbolInfo = result ?? string.Empty;
	}

	public async Task ApplyAiSuggestion(string aiCode)
	{
		var result = await _mergerService.MergeAiSnippetAsync(aiCode);
		if (result.Success && this.FilePath == result.FilePath)
		{
			_ = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
			{
				this.Rows.Clear();
				foreach (var block in result.MergedBlocks)
				{
					// Re-attach listener
					AttachBlockEvents(block);
					this.Rows.Add(new SegmentRowViewModel(block));
				}
				NotifyUnsavedChanges();
				if (IsComparisonMode)
					_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
			});
		}
	}

	public void ApplyExternalMerge(List<CodeBlockItem> mergedBlocks)
	{
		_ = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			Rows.Clear();
			foreach (var block in mergedBlocks)
			{
				AttachBlockEvents(block);
				Rows.Add(new SegmentRowViewModel(block));
			}
			NotifyUnsavedChanges();
			if (IsComparisonMode)
			{
				_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
			}
		});
	}

	public async Task SaveToDiskAsync()
	{
		try
		{
			IsLoading = true;
			var sb = new StringBuilder();
			foreach (var row in Rows)
			{
				sb.Append(row.Current.Content);
			}

			await _fileSystemService.SaveFileContentAsync(FilePath, sb.ToString());
			TriggerGlobalSave();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao salvar em disco: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}
	private void AttachBlockEvents(CodeBlockItem block)
	{
		block.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(CodeBlockItem.IsSelected))
			{
                // SYNC WITH MANAGER
                if (block.IsSelected)
                    _selectionManager.SelectBlock(FilePath, block.StableId);
                else
                    _selectionManager.DeselectBlock(FilePath, block.StableId);

				OnPropertyChanged(nameof(HasAnyBlockSelected));
				if (block.IsSelected)
				{
					FileSelectionRequested?.Invoke(this, EventArgs.Empty);
				}
			}
		};
	}
}