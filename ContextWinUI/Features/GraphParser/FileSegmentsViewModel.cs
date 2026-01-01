using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
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

	// --- CORREÇÃO DO LAYOUT CYCLE EXCEPTION ---

	// 1. Propriedade Observável Estável (Não use => aqui)
	// Inicializa com 0 (escondido)
	[ObservableProperty]
	private GridLength leftColumnWidth = new GridLength(0);

	// 2. Propriedade que controla o modo, agora dispara a atualização da largura
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
		int initialGlobalIndex)
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_symbolService = symbolService;
		_diffManager = diffManager;
		GlobalHistory = sharedHistory;
		CurrentGlobalIndex = initialGlobalIndex;

		// Inicia carregamento
		_ = LoadBlocksAsync();
	}

	// --- Métodos "Partial" (Hooks de Mudança de Propriedade) ---

	// Este é o método CRÍTICO que corrigimos.
	// Ao invés de o XAML recalcular a largura a cada frame, nós setamos ela UMA vez aqui.
	partial void OnIsComparisonModeChanged(bool value)
	{
		// Define a largura da coluna: 1* se estiver comparando, 0 se estiver editando.
		LeftColumnWidth = value ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

		if (value)
		{
			// Ativa lógica de Diff
			_diffManager.RefreshReferenceColumns(Rows, GlobalHistory, CompareLeftIndex);
			_diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
		}
		else
		{
			// Reseta visibilidade (Modo Edição sempre mostra tudo)
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
			var fileContent = await _fileSystemService.ReadFileContentAsync(FilePath);
			if (string.IsNullOrEmpty(fileContent)) return;

			var parsedItems = await _parserService.ParseFileAsync(FilePath, fileContent);

			foreach (var item in parsedItems)
			{
				Rows.Add(new SegmentRowViewModel(item));
			}

			if (Rows.Any()) SelectedBlock = Rows.First().Current;

			// Aplica estado inicial (histórico global)
			RestoreToGlobalIndex(CurrentGlobalIndex);
		}
		catch (Exception ex)
		{
			var errorBlock = new CodeBlockItem
			{
				Name = "Erro de Leitura",
				Content = ex.Message,
				SegmentType = SegmentType.Trivia,
				TypeDescription = "ERROR"
			};
			Rows.Add(new SegmentRowViewModel(errorBlock));
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
		var parentRow = Rows.FirstOrDefault(r => r.Current == referenceBlock);
		if (parentRow == null) return;

		int index = Rows.IndexOf(parentRow);

		// Cria novo bloco
		var newBlock = new CodeBlockItem
		{
			Name = "NovaFuncionalidade",
			SegmentType = SegmentType.Method,
			SymbolType = SymbolType.Method,
			DepthLevel = referenceBlock.DepthLevel,
			FileExtension = referenceBlock.FileExtension,
			TypeDescription = "NOVO MÉTODO",
			Content = $"\n{new string('\t', referenceBlock.DepthLevel)}// Novo Código..."
		};
		newBlock.InitializeVersions(string.Empty);

		var newRow = new SegmentRowViewModel(newBlock);
		Rows.Insert(index + 1, newRow);

		SelectedBlock = newBlock;
		NotifyUnsavedChanges();

		if (IsComparisonMode) _diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
	}

	[RelayCommand]
	public void DeleteBlock(CodeBlockItem? block)
	{
		if (block == null) return;
		var row = Rows.FirstOrDefault(r => r.Current == block);
		if (row != null)
		{
			// Ajusta seleção antes de remover
			int idx = Rows.IndexOf(row);
			if (SelectedBlock == block)
			{
				if (idx > 0) SelectedBlock = Rows[idx - 1].Current;
				else if (Rows.Count > 1) SelectedBlock = Rows[idx + 1].Current;
				else SelectedBlock = null;
			}

			Rows.Remove(row);
			NotifyUnsavedChanges();
		}
	}

	[RelayCommand]
	public void RevertBlockToOriginal(CodeBlockItem? currentBlock)
	{
		if (currentBlock != null && currentBlock.RestoreOriginal())
		{
			NotifyUnsavedChanges();
			if (IsComparisonMode) _diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
		}
	}

	[RelayCommand]
	public void PromoteEditToNewBlock(CodeBlockItem? currentBlock)
	{
		if (currentBlock == null) return;

		// Lógica de "Fork" do bloco
		currentBlock.Id = Guid.NewGuid().ToString();
		currentBlock.InitializeVersions(currentBlock.Content);
		currentBlock.TypeDescription += " (Novo)";
		currentBlock.Name += " *";

		var row = Rows.FirstOrDefault(r => r.Current == currentBlock);
		if (row != null) row.Reference = null;

		NotifyUnsavedChanges();
		if (IsComparisonMode) _diffManager.UpdateRowsVisibility(Rows, HideUnchangedBlocks);
	}

	// --- Persistência e Histórico ---

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
	public void CommitAllPendingChanges()
	{
		TriggerGlobalSave();
	}

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

	// --- Helpers de Notificação ---
	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	// --- Comunicação com Pai ---
	public void SyncGlobalIndex(int index) => SetProperty(ref currentGlobalIndex, index, nameof(CurrentGlobalIndex));
	partial void OnCurrentGlobalIndexChanged(int value) => GlobalRestoreRequested?.Invoke(this, value);
	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);
	public void TriggerGlobalRestore(int index) => GlobalRestoreRequested?.Invoke(this, index);
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
}