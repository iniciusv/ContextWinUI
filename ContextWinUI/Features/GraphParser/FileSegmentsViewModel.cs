using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml; // Necessário para GridLength
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

// Certifique-se de que a classe SegmentRowViewModel existe no mesmo namespace
// ou está acessível (definida no passo anterior).

public partial class FileSegmentsViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	private readonly ISemanticIndexService _indexService;
	private readonly ICodeBlockParserService _parserService;

	public string FilePath { get; }
	public string FileName { get; }


	[ObservableProperty]
	private ObservableCollection<SegmentRowViewModel> rows = new();

	// Propriedade auxiliar para manter compatibilidade com linq queries que buscam apenas os blocos atuais
	public IEnumerable<CodeBlockItem> Blocks => Rows.Select(r => r.Current);

	// --- Histórico Global ---
	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory;

	// --- Estado de Seleção e UI ---
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
	private CodeBlockItem? selectedBlock;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isEmpty;

	[ObservableProperty]
	private string currentSymbolInfo = string.Empty;

	// --- Estado de Comparação e Filtro ---
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(LeftColumnWidth))]
	private bool isComparisonMode;

	[ObservableProperty]
	private bool hideUnchangedBlocks;

	[ObservableProperty]
	private int compareLeftIndex = 0;

	[ObservableProperty]
	private int currentGlobalIndex = 0;

	// --- Propriedades Calculadas ---
	public bool HasSelectedBlock => SelectedBlock != null;

	// Varre as linhas para saber se algum bloco atual tem mudanças
	public bool HasAnyUnsavedChanges => Rows.Any(r => r.Current.HasUnsavedChanges);

	// Controla a largura da coluna esquerda (1* se ativo, 0 se inativo)
	public GridLength LeftColumnWidth => IsComparisonMode ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

	// --- Eventos ---
	public event EventHandler? GlobalSaveRequested;
	public event EventHandler<int>? GlobalRestoreRequested;

	public FileSegmentsViewModel(
		string filePath,
		IFileSystemService fileSystemService,
		SemanticIndexService indexService,
		ICodeBlockParserService parserService,
		ObservableCollection<GlobalVersion> sharedHistory,
		int initialGlobalIndex)
	{
		FilePath = filePath;
		FileName = Path.GetFileName(filePath);
		_fileSystemService = fileSystemService;
		_indexService = indexService;
		_parserService = parserService;
		GlobalHistory = sharedHistory;
		CurrentGlobalIndex = initialGlobalIndex;

		_ = LoadBlocksAsync();
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

			// Popula a lista de LINHAS
			foreach (var item in parsedItems)
			{
				Rows.Add(new SegmentRowViewModel(item));
			}

			if (Rows.Any())
			{
				SelectedBlock = Rows.First().Current;
			}

			// Restaura para o estado global atual solicitado na abertura
			RestoreToGlobalIndex(CurrentGlobalIndex);
		}
		catch (Exception ex)
		{
			// Fallback em caso de erro crítico no parse
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

	// --- Lógica de Comparação (Diff) ---

	partial void OnIsComparisonModeChanged(bool value)
	{
		if (value)
		{
			// 1. Preenche a propriedade .Reference de cada linha
			RefreshReferenceColumns(CompareLeftIndex);
			// 2. Aplica filtro de visibilidade (esconde o que for igual se a flag estiver ativa)
			UpdateRowsVisibility();
		}
		else
		{
			// Saiu do modo comparação: Mostra tudo e limpa referências para economizar memória (opcional)
			foreach (var row in Rows)
			{
				row.IsVisible = true;
				// Opcional: row.Reference = null; 
			}
		}
	}

	partial void OnCompareLeftIndexChanged(int value)
	{
		if (IsComparisonMode)
		{
			RefreshReferenceColumns(value);
			// Recalcula visibilidade pois a referência mudou, então o status de "igual" pode ter mudado
			if (HideUnchangedBlocks) UpdateRowsVisibility();
		}
	}

	partial void OnHideUnchangedBlocksChanged(bool value)
	{
		UpdateRowsVisibility();
	}

	private void UpdateRowsVisibility()
	{
		foreach (var row in Rows)
		{
			if (!HideUnchangedBlocks)
			{
				row.IsVisible = true;
			}
			else
			{
				// Mostra se:
				// 1. Tem mudanças não salvas (Edição em andamento)
				// 2. É um bloco novo (Versions <= 1)
				// 3. (Opcional) Se o conteúdo da referência é diferente do atual

				var block = row.Current;
				bool isModified = block.HasUnsavedChanges || block.Versions.Count <= 1;

				// Opcional: Checagem de conteúdo exato
				if (!isModified && row.Reference != null)
				{
					isModified = block.Content != row.Reference.Content;
				}

				row.IsVisible = isModified;
			}
		}
	}

	private void RefreshReferenceColumns(int globalVersionIndex)
	{
		var targetGlobalVersion = GlobalHistory.ElementAtOrDefault(globalVersionIndex);
		if (targetGlobalVersion == null) return;

		DateTime cutoffTime = targetGlobalVersion.IsOriginal ? DateTime.MinValue : targetGlobalVersion.Timestamp;
		if (cutoffTime > DateTime.MinValue) cutoffTime = cutoffTime.AddMilliseconds(100);

		foreach (var row in Rows)
		{
			var currentBlock = row.Current;

			// Busca a versão deste bloco que existia na data solicitada
			var pastVersion = currentBlock.Versions.LastOrDefault(v =>
				v.IsOriginal || v.Timestamp <= cutoffTime);

			if (pastVersion != null)
			{
				// Cria um clone para exibição na esquerda
				var refBlock = currentBlock.Clone();
				refBlock.Content = pastVersion.Content;
				// Trava o histórico do clone
				refBlock.InitializeVersions(pastVersion.Content);

				refBlock.TypeDescription = "REF";
				if (pastVersion.IsOriginal) refBlock.Name += " (Orig)";

				// Atualiza a ViewModel da linha
				row.Reference = refBlock;
			}
			else
			{
				// O bloco não existia nesta versão histórica (é um bloco novo)
				// Reference = null fará a coluna esquerda ficar vazia/oculta visualmente
				row.Reference = null;
			}
		}
	}

	// --- Manipulação de Blocos (Add/Delete/Edit) ---

	[RelayCommand]
	public void AddSiblingBlock(CodeBlockItem? referenceBlock)
	{
		if (referenceBlock == null) return;

		// Localiza a linha que contém o bloco de referência
		var parentRow = Rows.FirstOrDefault(r => r.Current == referenceBlock);
		if (parentRow == null) return;

		int index = Rows.IndexOf(parentRow);

		string indentation = new string('\t', referenceBlock.DepthLevel);
		string defaultContent = $"\n{indentation}// Novo Segmento\n{indentation}public void NovaFuncionalidade()\n{indentation}{{\n{indentation}\t\n{indentation}}}";

		var newBlock = new CodeBlockItem
		{
			Id = Guid.NewGuid().ToString(),
			Name = "NovaFuncionalidade",
			SegmentType = SegmentType.Method,
			SymbolType = SymbolType.Method,
			DepthLevel = referenceBlock.DepthLevel,
			FileExtension = referenceBlock.FileExtension,
			TypeDescription = "NOVO MÉTODO"
		};

		// Inicializa histórico VAZIO para que apareça como "Adição" no diff
		newBlock.InitializeVersions(string.Empty);
		// Define conteúdo, marcando como UnsavedChanges
		newBlock.Content = defaultContent;

		// Cria a nova LINHA
		var newRow = new SegmentRowViewModel(newBlock);

		// Em modo comparação, garantimos que a referência é null
		if (IsComparisonMode) newRow.Reference = null;

		// Insere na coleção
		Rows.Insert(index + 1, newRow);

		SelectedBlock = newBlock;
		NotifyUnsavedChanges();

		if (IsComparisonMode) UpdateRowsVisibility();
	}

	[RelayCommand]
	public void DeleteBlock(CodeBlockItem? block)
	{
		if (block == null) return;

		var row = Rows.FirstOrDefault(r => r.Current == block);
		if (row != null)
		{
			// Tenta selecionar vizinho antes de deletar
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
			NotifyChangesChanged();
			// Se reverteu, pode ter ficado igual à referência, então atualiza visibilidade
			if (IsComparisonMode) UpdateRowsVisibility();
		}
	}

	[RelayCommand]
	public void PromoteEditToNewBlock(CodeBlockItem? currentBlock)
	{
		if (currentBlock == null) return;

		// Quebra o link de histórico
		currentBlock.Id = Guid.NewGuid().ToString();
		// O conteúdo atual vira o novo "Original"
		currentBlock.InitializeVersions(currentBlock.Content);

		currentBlock.TypeDescription += " (Novo)";
		currentBlock.Name += " *";

		// Se está em modo comparação, a referência (que era do bloco antigo) não faz mais sentido
		// para este novo bloco. A referência deve virar null (ou o conteúdo atual, dependendo da semântica desejada)
		var row = Rows.FirstOrDefault(r => r.Current == currentBlock);
		if (row != null)
		{
			// Opção A: Zera a referência (trata como bloco 100% novo)
			row.Reference = null;
		}

		NotifyUnsavedChanges();
		NotifyChangesChanged();
		if (IsComparisonMode) UpdateRowsVisibility();
	}

	// --- Gerenciamento de Versão Global ---

	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	// Chamado pelo Orchestrator/Pai quando faz um Commit global
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

	public void RestoreToGlobalIndex(int globalIndex)
	{
		if (GlobalHistory == null || globalIndex < 0 || globalIndex >= GlobalHistory.Count) return;

		CurrentGlobalIndex = globalIndex;
		var targetGlobalVersion = GlobalHistory[globalIndex];

		var cutoffTime = targetGlobalVersion.IsOriginal
			? DateTime.MinValue
			: targetGlobalVersion.Timestamp;

		var safeCutoff = cutoffTime == DateTime.MinValue ? DateTime.MinValue : cutoffTime.AddMilliseconds(100);

		foreach (var row in Rows)
		{
			var block = row.Current;

			var bestVersion = block.Versions.LastOrDefault(v =>
				v.IsOriginal ||
				v.Timestamp <= safeCutoff);

			if (bestVersion != null)
			{
				int index = block.Versions.IndexOf(bestVersion);
				block.RestoreVersion(index);
			}
			else
			{
				block.RestoreVersion(0);
			}
		}

		OnPropertyChanged(nameof(HasAnyUnsavedChanges));

		// Se estivermos comparando, atualiza também a coluna de referência
		if (IsComparisonMode) RefreshReferenceColumns(CompareLeftIndex);
	}

	public void SyncGlobalIndex(int index)
	{
		if (CurrentGlobalIndex != index)
		{
			SetProperty(ref currentGlobalIndex, index, nameof(CurrentGlobalIndex));
		}
	}

	partial void OnCurrentGlobalIndexChanged(int value)
	{
		GlobalRestoreRequested?.Invoke(this, value);
	}

	[RelayCommand]
	public void SaveChanges(string mode)
	{
		if (mode == "NewVersion")
		{
			CommitGlobalVersion("Versão Manual");
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

					// Recarrega para limpar flags
					block.RestoreVersion(block.CurrentVersionIndex);
				}
			}
			NotifyUnsavedChanges();
		}
	}

	public void CommitGlobalVersion(string description)
	{
		TriggerGlobalSave();
	}

	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);
	public void TriggerGlobalRestore(int index) => GlobalRestoreRequested?.Invoke(this, index);

	// --- Lógica de Símbolos ---

	public void ResolveSymbolHeuristic(int cursorIndexInBlock)
	{
		if (SelectedBlock == null) return;

		string word = GetWordAtCursor(SelectedBlock.Content, cursorIndexInBlock);
		if (string.IsNullOrEmpty(word)) return;

		int absPos = SelectedBlock.AbsoluteStartPosition + cursorIndexInBlock;
		var node = _indexService.InferSymbolFromGraph(word, FilePath, absPos);

		if (node != null)
		{
			var graph = _indexService.GetCurrentGraph();
			string resolvedPath = graph.GetFilePath(node.FileId);
			string fileName = string.IsNullOrEmpty(resolvedPath) ? "Desconhecido" : Path.GetFileName(resolvedPath);
			CurrentSymbolInfo = $"[{node.Type}] {node.Name}\nDefinido em: {fileName}";
		}
		else
		{
			CurrentSymbolInfo = $"'{word}' (Sem informações no grafo)";
		}
	}

	private string GetWordAtCursor(string text, int position)
	{
		if (string.IsNullOrEmpty(text) || position < 0 || position > text.Length) return string.Empty;

		int start = position;
		int end = position;

		while (start > 0 && start <= text.Length && IsIdentifierChar(text[start - 1])) start--;
		while (end < text.Length && IsIdentifierChar(text[end])) end++;

		if (start >= end) return string.Empty;
		return text.Substring(start, end - start);
	}

	private bool IsIdentifierChar(char c)
	{
		return char.IsLetterOrDigit(c) || c == '_';
	}
}