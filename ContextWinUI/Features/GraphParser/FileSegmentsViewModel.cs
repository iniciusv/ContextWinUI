using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.Services;
using Microsoft.UI.Xaml; // Para GridLength
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class FileSegmentsViewModel : ObservableObject
{
	// --- Dependências Injetadas ---
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;
	private readonly ISymbolResolutionService _symbolService;
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _mergerService;
	private readonly IBlockEditorService _editorService;
	private readonly IBlockLoaderService _loaderService;


	// --- Propriedades Básicas ---
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

	// Propriedades Computadas Simples
	public bool HasSelectedBlock => SelectedBlock != null;
	public bool HasAnyUnsavedChanges => Rows.Any(r => r.Current.HasUnsavedChanges);

	// Eventos
	public event EventHandler? GlobalSaveRequested;
	public event EventHandler<int>? GlobalRestoreRequested;

	// --- Construtor ---
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
		IBlockEditorService editorService)
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

	private async Task LoadBlocksAsync()
	{
		IsLoading = true;
		Rows.Clear();
		SelectedBlock = null;
		try
		{
			// O serviço faz todo o trabalho pesado
			var items = await _loaderService.LoadAndProcessFileAsync(FilePath);

			foreach (var item in items)
			{
				Rows.Add(new SegmentRowViewModel(item));
			}

			if (Rows.Any()) SelectedBlock = Rows.First().Current;
			RestoreToGlobalIndex(CurrentGlobalIndex);
		}
		catch (Exception ex)
		{
			// Tratamento de erro de UI continua aqui
			Rows.Add(new SegmentRowViewModel(new CodeBlockItem
			{
				Name = "Erro",
				Content = ex.Message,
				SegmentType = SegmentType.Trivia
			}));
		}
		finally
		{
			IsLoading = false;
			IsEmpty = Rows.Count == 0;
		}
	}

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

		// Delega para o serviço (que retorna true se algo mudou)
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
			else if (Rows.Count > 1) SelectedBlock = Rows[idx + 1].Current; // Pega o próximo já que o atual vai mudar/sumir
			else SelectedBlock = null;
		}
	}

	[RelayCommand]
	public void SaveChanges(string mode)
	{
		if (mode == "NewVersion")
		{
			TriggerGlobalSave(); // Delega para o Pai criar versão global
		}
		else if (mode == "Overwrite")
		{
			foreach (var row in Rows)
			{
				var block = row.Current;
				if (block.HasUnsavedChanges && block.Versions.Any())
				{
					// Sobrescreve a versão atual na memória (timestamp update)
					var currentVer = block.Versions[block.CurrentVersionIndex];
					currentVer.Content = block.Content;
					currentVer.Timestamp = DateTime.Now;

					// Reseta o estado de "não salvo"
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

		// Delega o cálculo complexo de timestamps e versões
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

		// O ViewModel DELEGA a lógica "suja" para o serviço
		var result = _symbolService.ResolveSymbolAtPosition(
			SelectedBlock.Content,
			cursorIndexInBlock,
			SelectedBlock.AbsoluteStartPosition, // Posição absoluta no arquivo original
			FilePath
		);

		// Atualiza a propriedade que a UI está observando
		CurrentSymbolInfo = result ?? string.Empty;
	}

	public async Task ApplyAiSuggestion(string aiCode)
	{
		var result = await _mergerService.MergeAiSnippetAsync(aiCode);

		if (result.Success && this.FilePath == result.FilePath)
		{
			// Atualiza a UI na Thread Principal
			_ = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
			{
				this.Rows.Clear();
				foreach (var block in result.MergedBlocks)
				{
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
		// Executa na thread de UI para garantir segurança
		_ = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
		{
			Rows.Clear();
			foreach (var block in mergedBlocks)
			{
				Rows.Add(new SegmentRowViewModel(block));
			}

			// Marca que houve alteração para a UI reagir (ícones de disquete, diff, etc)
			NotifyUnsavedChanges();

			// Se estiver no modo de comparação, atualiza a visualização
			if (IsComparisonMode)
			{
				_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
			}
		});
	}
}