// ARQUIVO: AiChangesViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class AiChangesViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IProjectSessionManager _sessionManager;
	private readonly SemanticIndexService _semanticIndexService;

	// Serviços de processamento
	private readonly AiResponseParser _parser = new();
	private readonly RoslynSemanticDiffService _semanticDiffService = new();
	private readonly SemanticMergeService _semanticMergeService = new();

	// Estado interno
	private string _lastProcessedFile = string.Empty;

	[ObservableProperty]
	private string rawInput = string.Empty;

	[ObservableProperty]
	private ObservableCollection<ProposedFileChange> detectedChanges = new();

	[ObservableProperty]
	private ProposedFileChange? selectedChange;

	[ObservableProperty]
	private bool isProcessing;

	[ObservableProperty]
	private ObservableCollection<ModificationHistoryEntry> history = new();

	// Coleção principal para a UI: Lista de Métodos/Propriedades alterados
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasSemanticBlocks))]
	private ObservableCollection<SemanticChangeBlock> semanticBlocks = new();

	public bool HasSemanticBlocks => SemanticBlocks != null && SemanticBlocks.Any();

	public AiChangesViewModel(IFileSystemService fileSystemService, IProjectSessionManager sessionManager, SemanticIndexService semanticIndexService)
	{
		_fileSystemService = fileSystemService;
		_sessionManager = sessionManager;
		_semanticIndexService = semanticIndexService;
	}

	// Trigger automático ao selecionar um arquivo na lista lateral
	partial void OnSelectedChangeChanged(ProposedFileChange? value)
	{
		if (value != null)
		{
			_ = GenerateDiffForSelectedAsync();
		}
		else
		{
			SemanticBlocks.Clear();
		}
	}

	[RelayCommand]
	private async Task ProcessInputAsync()
	{
		if (string.IsNullOrWhiteSpace(RawInput) || string.IsNullOrEmpty(_sessionManager.CurrentProjectPath))
			return;

		IsProcessing = true;
		DetectedChanges.Clear();
		SemanticBlocks.Clear();
		SelectedChange = null;

		try
		{
			var projectRoot = _sessionManager.CurrentProjectPath;
			var currentGraph = await _semanticIndexService.GetOrIndexProjectAsync(projectRoot);

			// Parser extrai os arquivos do texto da IA
			var changes = await Task.Run(() => _parser.ParseInput(RawInput, projectRoot, currentGraph));

			foreach (var change in changes)
			{
				// Se o arquivo existe, carregamos o original para comparação
				if (string.IsNullOrEmpty(change.OriginalContent) && File.Exists(change.FilePath))
				{
					change.OriginalContent = await _fileSystemService.ReadFileContentAsync(change.FilePath);
				}

				DetectedChanges.Add(change);
			}

			if (DetectedChanges.Any())
			{
				SelectedChange = DetectedChanges.First();
			}
		}
		catch (Exception ex)
		{
			DetectedChanges.Add(new ProposedFileChange
			{
				FilePath = "Erro de Processamento",
				NewContent = ex.ToString(),
				Status = "Erro"
			});
		}
		finally
		{
			IsProcessing = false;
		}
	}

	public async Task GenerateDiffForSelectedAsync()
	{
		if (SelectedChange == null) return;

		// Evita reprocessar se já estamos mostrando este arquivo
		if (_lastProcessedFile == SelectedChange.FilePath && SemanticBlocks.Any() && !isProcessing)
			return;

		IsProcessing = true;
		try
		{
			var graph = _semanticIndexService.GetCurrentGraph();

			// O Serviço Roslyn compara a estrutura e retorna blocos (métodos, props)
			var blocks = await _semanticDiffService.ComputeSemanticDiffAsync(
				SelectedChange.OriginalContent ?? "",
				SelectedChange.NewContent ?? "",
				graph
			);

			SemanticBlocks.Clear();
			foreach (var b in blocks)
			{
				SemanticBlocks.Add(b);
			}

			_lastProcessedFile = SelectedChange.FilePath;
			OnPropertyChanged(nameof(HasSemanticBlocks));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro ao gerar diff semântico: {ex}");
			// Fallback simples em caso de erro crítico no Roslyn
			SemanticBlocks.Clear();
			var errLine = new DiffLine { Text = "Erro ao analisar estrutura: " + ex.Message, Type = DiffType.Deleted };
			SemanticBlocks.Add(new SemanticChangeBlock("Erro", "\uE783", DiffType.Deleted, new[] { errLine }));
		}
		finally
		{
			IsProcessing = false;
		}
	}

	[RelayCommand]
	private async Task ApplySelectedChangesAsync()
	{
		// Aplica APENAS o arquivo selecionado atualmente e seus blocos marcados
		// (Pode ser expandido para aplicar em lote se necessário)
		if (SelectedChange == null) return;

		IsProcessing = true;
		try
		{
			// Salva histórico para Undo
			var historyEntry = new ModificationHistoryEntry(SelectedChange.FilePath, SelectedChange.OriginalContent);
			History.Insert(0, historyEntry);

			string finalContent;

			// Se temos blocos semânticos, usamos o Merge inteligente
			if (SemanticBlocks.Any())
			{
				// Pega apenas os blocos que o usuário deixou marcados (Checkbox = True)
				var blocksToApply = SemanticBlocks.Where(b => b.IsSelected).ToList();

				// O MergeService pega o arquivo original e substitui/insere apenas esses blocos
				finalContent = await _semanticMergeService.MergeBlocksAsync(
					SelectedChange.OriginalContent,
					blocksToApply,
					_sessionManager.CurrentProjectPath // Passar path se precisar resolver usings
				);
			}
			else
			{
				// Fallback: Se não houve análise semântica, aplica o texto inteiro
				finalContent = SelectedChange.NewContent;
			}

			await _fileSystemService.SaveFileContentAsync(SelectedChange.FilePath, finalContent);

			// Atualiza estado
			SelectedChange.Status = "Aplicado";
			SelectedChange.OriginalContent = finalContent; // O novo original é o que acabamos de salvar

			// Regenera o diff (deve ficar vazio ou mostrar diferenças residuais se houver)
			await GenerateDiffForSelectedAsync();
		}
		catch (Exception ex)
		{
			SelectedChange.Status = $"Erro ao salvar: {ex.Message}";
		}
		finally
		{
			IsProcessing = false;
		}
	}

	[RelayCommand]
	private async Task RevertChangeAsync(ModificationHistoryEntry entry)
	{
		if (entry == null) return;
		IsProcessing = true;
		try
		{
			await _fileSystemService.SaveFileContentAsync(entry.FilePath, entry.PreviousContent);
			History.Remove(entry);

			// Se o arquivo revertido for o atual selecionado, atualiza a view
			if (SelectedChange != null && SelectedChange.FilePath == entry.FilePath)
			{
				SelectedChange.OriginalContent = entry.PreviousContent;
				SelectedChange.Status = "Revertido";
				await GenerateDiffForSelectedAsync();
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Erro ao reverter: {ex}");
		}
		finally
		{
			IsProcessing = false;
		}
	}

	[RelayCommand]
	private void Clear()
	{
		RawInput = string.Empty;
		DetectedChanges.Clear();
		SemanticBlocks.Clear();
		SelectedChange = null;
		_lastProcessedFile = string.Empty;
	}
}