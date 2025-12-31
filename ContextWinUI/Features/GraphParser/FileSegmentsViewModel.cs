using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Services;
using Microsoft.UI.Xaml; // Necessário para GridLength
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class FileSegmentsViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	private readonly ISemanticIndexService _indexService;
	private readonly ICodeBlockParserService _parserService;

	public string FilePath { get; }
	public string FileName { get; }

	// --- Coleções ---

	// Lista principal (editável - Lado Direito)
	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> blocks = new();

	// Histórico global do arquivo
	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory;

	// Lista de referência (somente leitura - Lado Esquerdo)
	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> referenceBlocks = new();

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

	// Qual versão global estamos mostrando no lado esquerdo?
	[ObservableProperty]
	private int compareLeftIndex = 0;

	// Qual versão global estamos visualizando/editando no lado direito?
	[ObservableProperty]
	private int currentGlobalIndex = 0;

	// --- Propriedades Calculadas ---
	public bool HasSelectedBlock => SelectedBlock != null;
	public bool HasAnyUnsavedChanges => Blocks.Any(b => b.HasUnsavedChanges);

	// Controla a largura da coluna esquerda (1* se ativo, 0 se inativo)
	// Permite que a View oculte a coluna sem usar conversores complexos
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
		Blocks.Clear();
		SelectedBlock = null;

		try
		{
			var fileContent = await _fileSystemService.ReadFileContentAsync(FilePath);
			if (string.IsNullOrEmpty(fileContent)) return;

			var parsedItems = await _parserService.ParseFileAsync(FilePath, fileContent);

			foreach (var item in parsedItems)
			{
				Blocks.Add(item);
			}

			if (Blocks.Any())
			{
				SelectedBlock = Blocks.First();
			}

			// Restaura para o estado global atual solicitado na abertura
			RestoreToGlobalIndex(CurrentGlobalIndex);
		}
		catch (Exception ex)
		{
			Blocks.Add(new CodeBlockItem
			{
				Name = "Erro de Leitura",
				Content = ex.Message,
				SegmentType = SegmentType.Trivia,
				TypeDescription = "ERROR"
			});
		}
		finally
		{
			IsLoading = false;
			IsEmpty = Blocks.Count == 0;
		}
	}

	// --- Lógica de Comparação (Diff) ---

	partial void OnIsComparisonModeChanged(bool value)
	{
		if (value)
		{
			// Carrega os dados da esquerda
			LoadReferenceBlocks(CompareLeftIndex);
			// Aplica filtro de visibilidade (caso HideUnchangedBlocks esteja true)
			UpdateBlocksVisibility();
		}
		else
		{
			// Ao sair do modo comparação, garante que todos os blocos do editor estejam visíveis
			foreach (var block in Blocks) block.IsVisibleInDiff = true;
		}
	}

	partial void OnCompareLeftIndexChanged(int value)
	{
		if (IsComparisonMode)
		{
			LoadReferenceBlocks(value);
		}
	}

	partial void OnHideUnchangedBlocksChanged(bool value)
	{
		UpdateBlocksVisibility();
	}

	private void UpdateBlocksVisibility()
	{
		// Itera sobre todos os blocos para definir se devem aparecer na lista da direita
		foreach (var block in Blocks)
		{
			if (!HideUnchangedBlocks)
			{
				block.IsVisibleInDiff = true;
			}
			else
			{
				// Mostra se tem mudanças não salvas OU se é um bloco novo (apenas 1 versão no histórico)
				block.IsVisibleInDiff = block.HasUnsavedChanges || block.Versions.Count <= 1;
			}
		}
	}

	private void LoadReferenceBlocks(int globalVersionIndex)
	{
		ReferenceBlocks.Clear();

		var targetGlobalVersion = GlobalHistory.ElementAtOrDefault(globalVersionIndex);
		if (targetGlobalVersion == null) return;

		// Define o tempo de corte para buscar a versão histórica
		DateTime cutoffTime = targetGlobalVersion.IsOriginal ? DateTime.MinValue : targetGlobalVersion.Timestamp;
		// Pequena margem de segurança
		if (cutoffTime > DateTime.MinValue) cutoffTime = cutoffTime.AddMilliseconds(100);

		foreach (var currentBlock in Blocks)
		{
			// Tenta encontrar a versão deste bloco que existia na época da versão global selecionada
			var pastVersion = currentBlock.Versions.LastOrDefault(v =>
				v.IsOriginal || v.Timestamp <= cutoffTime);

			if (pastVersion != null)
			{
				// Criamos um clone visual para exibir na esquerda (não editável)
				var refBlock = currentBlock.Clone();
				refBlock.Content = pastVersion.Content;
				refBlock.TypeDescription = "REFERÊNCIA";

				if (pastVersion.IsOriginal) refBlock.Name += " (Original)";

				ReferenceBlocks.Add(refBlock);
			}
			// Se pastVersion for null, significa que este bloco não existia naquela época.
			// Ele simplesmente não aparecerá na lista da esquerda.
		}
	}

	// --- Manipulação de Blocos (Add/Delete/Edit) ---

	[RelayCommand]
	public void AddSiblingBlock(CodeBlockItem? referenceBlock)
	{
		if (referenceBlock == null) return;

		int index = Blocks.IndexOf(referenceBlock);
		if (index == -1) return;

		// Cria template baseado na indentação do irmão
		string indentation = new string('\t', referenceBlock.DepthLevel);
		string defaultContent = $"\n{indentation}// Novo Segmento\n{indentation}public void NovaFuncionalidade()\n{indentation}{{\n{indentation}\t\n{indentation}}}";

		var newBlock = new CodeBlockItem
		{
			Id = Guid.NewGuid().ToString(),
			Name = "NovaFuncionalidade",
			SegmentType = SegmentType.Method, // IsGranular será true automaticamente
			SymbolType = SymbolType.Method,
			DepthLevel = referenceBlock.DepthLevel,
			FileExtension = referenceBlock.FileExtension,
			TypeDescription = "NOVO MÉTODO"
		};

		// Inicializa histórico do novo bloco
		newBlock.InitializeVersions(defaultContent);

		// Insere na coleção
		Blocks.Insert(index + 1, newBlock);

		// Foca no novo bloco
		SelectedBlock = newBlock;
		NotifyUnsavedChanges();

		// Se estiver filtrando, garante que o novo bloco apareça
		if (IsComparisonMode) UpdateBlocksVisibility();
	}

	[RelayCommand]
	public void DeleteBlock(CodeBlockItem? block)
	{
		if (block == null) return;

		if (Blocks.Contains(block))
		{
			// Tenta manter a seleção em um vizinho
			if (SelectedBlock == block)
			{
				var index = Blocks.IndexOf(block);
				if (index > 0) SelectedBlock = Blocks[index - 1];
				else if (Blocks.Count > 1) SelectedBlock = Blocks[index + 1];
				else SelectedBlock = null;
			}

			Blocks.Remove(block);
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
			if (IsComparisonMode) UpdateBlocksVisibility();
		}
	}

	[RelayCommand]
	public void PromoteEditToNewBlock(CodeBlockItem? currentBlock)
	{
		if (currentBlock == null) return;

		// Transforma uma edição em um novo bloco (quebra o link de histórico com o bloco anterior)
		currentBlock.Id = Guid.NewGuid().ToString();

		// Reseta histórico: o conteúdo atual passa a ser o "Original" deste novo bloco
		currentBlock.InitializeVersions(currentBlock.Content);

		currentBlock.TypeDescription += " (Novo)";
		currentBlock.Name += " *";

		NotifyUnsavedChanges();
		NotifyChangesChanged();
		if (IsComparisonMode) UpdateBlocksVisibility();
	}

	// --- Gerenciamento de Versão Global ---

	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	// Chamado pelo Orchestrator/Pai quando faz um Commit global
	public void SnapshotBlocksForGlobalVersion(string description, DateTime batchTimestamp)
	{
		foreach (var block in Blocks)
		{
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

		foreach (var block in Blocks)
		{
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
				// Se não achou versão compatível, restaura a original (0)
				block.RestoreVersion(0);
			}
		}

		OnPropertyChanged(nameof(HasAnyUnsavedChanges));

		// Se estivermos comparando, atualiza a referência da esquerda para garantir consistência
		if (IsComparisonMode) LoadReferenceBlocks(CompareLeftIndex);
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

	// Método chamado pelo botão de Save no Footer
	// 'mode' vem do CommandParameter: "NewVersion" ou "Overwrite"
	[RelayCommand]
	public void SaveChanges(string mode)
	{
		if (mode == "NewVersion")
		{
			// Fluxo normal: Cria nova entrada no histórico global
			CommitGlobalVersion("Versão Manual");
		}
		else if (mode == "Overwrite")
		{
			// Fluxo Destrutivo: Atualiza a versão ATUAL dos blocos modificados
			foreach (var block in Blocks)
			{
				if (block.HasUnsavedChanges && block.Versions.Any())
				{
					var currentVer = block.Versions[block.CurrentVersionIndex];
					currentVer.Content = block.Content;
					currentVer.Timestamp = DateTime.Now; // Atualiza timestamp

					// Força a UI a reconhecer que não há mais "mudanças não salvas" (pois acabamos de salvar na versão atual)
					// Truque: Recarrega a própria versão para limpar flags
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

	// --- Lógica de Símbolos (Inalterada) ---

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