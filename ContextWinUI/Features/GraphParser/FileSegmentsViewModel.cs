using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml.Media;
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

	[ObservableProperty]
	private ObservableCollection<CodeBlockItem> blocks = new();

	// Referência ao histórico compartilhado do Pai
	[ObservableProperty]
	private ObservableCollection<GlobalVersion> globalHistory;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
	private CodeBlockItem? selectedBlock;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool isEmpty;

	// Sincronizado com o pai para controle do Footer
	[ObservableProperty]
	private int currentGlobalIndex = 0;

	[ObservableProperty]
	private string currentSymbolInfo = string.Empty;

	public bool HasSelectedBlock => SelectedBlock != null;
	public bool HasAnyUnsavedChanges => Blocks.Any(b => b.HasUnsavedChanges);

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

		// Recebe o estado global
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

			// Garante que a aba abra no estado global atual
			RestoreToGlobalIndex(CurrentGlobalIndex);

			InitializeGlobalHistory(); // Método local apenas para garantir estado interno se necessário
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

	private void InitializeGlobalHistory()
	{
		// Se precisar de alguma inicialização local, faz aqui.
		// Mas o histórico principal vem do GlobalHistory
	}

	public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

	// --- SNAPSHOT COM TIMESTAMP ---
	public void SnapshotBlocksForGlobalVersion(string description, DateTime batchTimestamp)
	{
		foreach (var block in Blocks)
		{
			if (block.HasUnsavedChanges)
			{
				// Passa o timestamp global para manter sincronia temporal
				block.CreateNewVersion(block.Content, description, batchTimestamp);
			}
		}
		OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	}

	// --- RESTORE COM TIMESTAMP ---
	public void RestoreToGlobalIndex(int globalIndex)
	{
		if (GlobalHistory == null || globalIndex < 0 || globalIndex >= GlobalHistory.Count) return;

		// Atualiza a propriedade sem disparar evento de loop (controlamos isso no OnChanged se necessário)
		CurrentGlobalIndex = globalIndex;

		var targetGlobalVersion = GlobalHistory[globalIndex];

		// Se for a versão original, voltamos ao início dos tempos
		// Se não, pegamos o timestamp da versão global
		var cutoffTime = targetGlobalVersion.IsOriginal
			? DateTime.MinValue
			: targetGlobalVersion.Timestamp;

		// Adicionamos margem de segurança pequena para erros de arredondamento de ticks
		var safeCutoff = cutoffTime == DateTime.MinValue ? DateTime.MinValue : cutoffTime.AddMilliseconds(100);

		foreach (var block in Blocks)
		{
			// Lógica Temporal:
			// "Quero a última versão deste bloco que existia na data X"
			var bestVersion = block.Versions.LastOrDefault(v =>
				v.IsOriginal || // Versão original é sempre válida se não houver outra
				v.Timestamp <= safeCutoff);

			if (bestVersion != null)
			{
				int index = block.Versions.IndexOf(bestVersion);
				// Chama o bloco para restaurar. Isso atualiza a propriedade Content, que atualiza a tela.
				block.RestoreVersion(index);
			}
			else
			{
				block.RestoreVersion(0);
			}
		}
		OnPropertyChanged(nameof(HasAnyUnsavedChanges));
	}

	// --- SINCRONIA VISUAL ---
	public void SyncGlobalIndex(int index)
	{
		// Atualiza apenas o número para o Footer ficar certo, sem rodar a lógica pesada de restore
		// (Assumimos que o restore já foi feito ou não é necessário neste contexto)
		if (CurrentGlobalIndex != index)
		{
			SetProperty(ref currentGlobalIndex, index, nameof(CurrentGlobalIndex));
		}
	}

	// --- COMANDO DO FOOTER (UI -> Pai) ---
	partial void OnCurrentGlobalIndexChanged(int value)
	{
		// Quando o usuário mexe no slider desta aba, avisamos o Pai.
		// O Pai vai chamar RestoreAllToVersion(value), que chamará RestoreToGlobalIndex(value) aqui.
		GlobalRestoreRequested?.Invoke(this, value);
	}

	public void CommitGlobalVersion(string description)
	{
		// Redireciona para o fluxo central
		TriggerGlobalSave();
	}

	public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);
	public void TriggerGlobalRestore(int index) => GlobalRestoreRequested?.Invoke(this, index);

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