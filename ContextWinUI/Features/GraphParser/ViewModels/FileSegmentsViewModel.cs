using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.Handlers;
using ContextWinUI.Features.GraphParser.Helpers;
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
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _mergerService;
	private readonly IBlockLoaderService _loaderService;
    private readonly IBlockSelectionManager _selectionManager;

    // Handlers
    private readonly FileSegmentsSelectionHandler _selectionHandler;
    private readonly FileSegmentsNavigationHandler _navigationHandler;
    private readonly FileSegmentsEditHandler _editHandler;
    private readonly FileSegmentsComparisonHandler _comparisonHandler;

	public event EventHandler? FileSelectionRequested;
	public event EventHandler? GlobalSaveRequested;
	public event EventHandler<int>? GlobalRestoreRequested;
    public event EventHandler<NavigationRequestArgs>? ReferenceNavigationRequested;
    public event EventHandler<CodeBlockItem>? ScrollRequested;

	public string FilePath { get; }
	public string FileName { get; }

    public IBlockSelectionManager SelectionManager => _selectionManager;

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
        IBlockSelectionManager selectionManager,
        IGitComparisonService gitComparisonService)
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_diffManager = diffManager;
		GlobalHistory = sharedHistory;
		CurrentGlobalIndex = initialGlobalIndex;
		_mergerService = mergerService;
		_loaderService = loaderService;
        _selectionManager = selectionManager;

        // Initialize Handlers
        _selectionHandler = new FileSegmentsSelectionHandler(this, selectionManager);
        _navigationHandler = new FileSegmentsNavigationHandler(this, symbolService, parserService);
        _editHandler = new FileSegmentsEditHandler(this, editorService);
        _comparisonHandler = new FileSegmentsComparisonHandler(this, diffManager, gitComparisonService);
        
        // Wire up Handler Events
        _selectionHandler.FileSelectionRequested += (s, e) => FileSelectionRequested?.Invoke(this, EventArgs.Empty);

        // Load
		_ = LoadBlocksAsync();
	}

    // Property Change Delegations
	partial void OnIsComparisonModeChanged(bool value) => _comparisonHandler.OnIsComparisonModeChanged(value);
	partial void OnCompareLeftIndexChanged(int value) => _comparisonHandler.OnCompareLeftIndexChanged(value);
	partial void OnHideUnchangedBlocksChanged(bool value) => RefreshRowsVisibility();
    partial void OnCurrentGlobalIndexChanged(int value) => GlobalRestoreRequested?.Invoke(this, value);

    // Display Options
    public bool ShowUsings { get; private set; } = true;
    public bool ShowTrivia { get; private set; } = true;

    public void SetDisplayOptions(bool showUsings, bool showTrivia)
    {
        if (ShowUsings == showUsings && ShowTrivia == showTrivia) return;
        ShowUsings = showUsings;
        ShowTrivia = showTrivia;
        RefreshRowsVisibility();
    }

    public void RefreshRowsVisibility()
    {
        // 1. Ask DiffManager to update visibility based on Diff status
        if (IsComparisonMode)
        {
             _diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
        }
        else
        {
             foreach(var r in Rows) r.IsVisible = true;
        }

        // 2. Apply Type Filter on top
        foreach (var row in Rows)
        {
            if (!row.IsVisible) continue; 

            if (!ShowUsings && (row.Current.SegmentType == SegmentType.Using || row.Current.SegmentType == SegmentType.FileHeader))
            {
                row.IsVisible = false;
            }
            else if (!ShowTrivia && row.Current.SegmentType == SegmentType.Trivia)
            {
                row.IsVisible = false;
            }
        }
    }

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

                _selectionHandler.InitializeSelection(item);
				_selectionHandler.AttachBlockEvents(item);

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
			}
            RefreshRowsVisibility();
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
		var selectedRows = Rows.Where(r => r.Current.IsSelected || r.Current.IsReferenced).ToList();

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

		var fullSb = new StringBuilder();
		fullSb.AppendLine($"// File: {FileName} (Full Content)");
		foreach (var row in Rows)
		{
			fullSb.Append(row.Current.Content);
		}
		return fullSb.ToString();
	}

    // Commands Delegated to Handlers
	[RelayCommand]
	public void AddSiblingBlock(CodeBlockItem? referenceBlock) => _editHandler.AddSiblingBlock(referenceBlock);

	[RelayCommand]
	public void DeleteBlock(CodeBlockItem? block) => _editHandler.DeleteBlock(block);

	[RelayCommand]
	public void LinkBlocks(Tuple<CodeBlockItem, CodeBlockItem> items) => _editHandler.LinkBlocks(items);

	[RelayCommand]
	public void RevertBlockToOriginal(CodeBlockItem? currentBlock) => _editHandler.RevertBlockToOriginal(currentBlock);

	[RelayCommand]
	public void PromoteEditToNewBlock(CodeBlockItem? currentBlock) => _editHandler.PromoteEditToNewBlock(currentBlock);
    
    // Interactions
    public void HandleBlockClick(CodeBlockItem block, bool isCtrlPressed) 
    {
        _selectionHandler.HandleBlockClick(block, isCtrlPressed);
        _navigationHandler.RefreshReferences(); 
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

    // Comparison / Git
	public void RestoreToGlobalIndex(int globalIndex) => _comparisonHandler.RestoreToGlobalIndex(globalIndex);

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
	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);

    // Navigation
	public async Task ResolveSymbolHeuristic(int cursorIndexInBlock) => await _navigationHandler.ResolveSymbolHeuristic(cursorIndexInBlock);
    public void OnSymbolNavigationRequested(int cursorIndexInBlock, bool isImplementation) => _navigationHandler.OnSymbolNavigationRequested(cursorIndexInBlock, isImplementation);

    public class NavigationRequestArgs : EventArgs
    {
        public string TargetFilePath { get; set; } = string.Empty;
        public int TargetPosition { get; set; }
    }

    // Exposed for Handlers
    public void TriggerScrollTo(CodeBlockItem item) => ScrollRequested?.Invoke(this, item);
    public void TriggerNavigationRequest(string filePath, int position) 
    {
        ReferenceNavigationRequested?.Invoke(this, new NavigationRequestArgs
        {
            TargetFilePath = filePath,
            TargetPosition = position
        });
    }

    [ObservableProperty]
    private ObservableCollection<SymbolResolutionResult> implementationCandidates = new();

    [ObservableProperty]
    private SymbolResolutionResult? selectedImplementation;

    partial void OnSelectedImplementationChanged(SymbolResolutionResult? value)
    {
        if (value != null)
        {
            TriggerNavigationRequest(value.FilePath, value.AbsolutePosition);
        }
    }

    // Merging
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
                    _selectionHandler.AttachBlockEvents(block);
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
                _selectionHandler.AttachBlockEvents(block);
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

    public async Task InitializeFromGitContent(string contentCurrent, string contentOld, string oldVersionLabel)
    {
        IsLoading = true;
        try
        {
            var result = await _comparisonHandler.InitializeFromGitContent(contentCurrent, contentOld, oldVersionLabel);
            
            foreach (var row in result.Rows)
            {
               _selectionHandler.AttachBlockEvents(row.Current);
            }
            Rows = result.Rows;
            
             // RefreshVisibility is likely done by OnIsComparisonModeChanged inside Handler which triggers property change here
             // But Rows setter might need visibility refresh if not auto.
             if (IsComparisonMode)
             {
                 _diffManager.RefreshReferenceColumns(Rows, GlobalHistory, CompareLeftIndex); 
             }
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"Error initializing from Git: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ScrollToPosition(int absolutePosition) => _navigationHandler.ScrollToPosition(absolutePosition);
}
