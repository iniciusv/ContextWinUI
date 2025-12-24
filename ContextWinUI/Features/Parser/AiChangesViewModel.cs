using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using ContextWinUI.Services; // Namespace onde está o RoslynSemanticDiffService
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class AiChangesViewModel : ObservableObject
{
	private readonly IFileSystemService _fileSystemService;
	private readonly IProjectSessionManager _sessionManager;
	private readonly SemanticIndexService _semanticIndexService;
	private readonly AiResponseParser _parser = new();

	// ALTERAÇÃO: Trocado TextDiffService por RoslynSemanticDiffService
	private readonly RoslynSemanticDiffService _semanticDiffService = new();

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

	[ObservableProperty]
	private ObservableCollection<DiffLine> diffLines = new();

	public AiChangesViewModel(IFileSystemService fileSystemService, IProjectSessionManager sessionManager, SemanticIndexService semanticIndexService)
	{
		_fileSystemService = fileSystemService;
		_sessionManager = sessionManager;
		_semanticIndexService = semanticIndexService;
	}

	// ALTERAÇÃO: Método hook gerado pelo CommunityToolkit para reagir à mudança de seleção.
	// Isso elimina a necessidade de código no Code-Behind da View.
	partial void OnSelectedChangeChanged(ProposedFileChange? value)
	{
		_ = GenerateDiffForSelectedAsync();
	}

	[RelayCommand]
	private async Task ProcessInputAsync()
	{
		if (string.IsNullOrWhiteSpace(RawInput) || string.IsNullOrEmpty(_sessionManager.CurrentProjectPath))
			return;

		IsProcessing = true;
		DetectedChanges.Clear();
		SelectedChange = null;

		try
		{
			var projectRoot = _sessionManager.CurrentProjectPath;
			var currentGraph = _semanticIndexService.GetCurrentGraph();

			var finalChangesList = new List<ProposedFileChange>();

			// O parser agora usa a lógica centralizada internamente
			var standardChanges = await Task.Run(() => _parser.ParseInput(RawInput, projectRoot, currentGraph));
			finalChangesList.AddRange(standardChanges);

			bool hasSnippetMarkers = RawInput.Contains("...");
			bool noResultsFound = !finalChangesList.Any();
			bool potentialDestructiveOverwrite = finalChangesList.Any(c => c.Status == "Novo Arquivo" && hasSnippetMarkers);

			if (hasSnippetMarkers || noResultsFound || potentialDestructiveOverwrite)
			{
				var patcher = new SmartSnippetPatcher(_semanticIndexService);
				var smartChange = await patcher.PatchAsync(RawInput, projectRoot);

				if (smartChange != null)
				{
					finalChangesList.RemoveAll(c => c.FilePath == smartChange.FilePath);
					finalChangesList.Add(smartChange);
				}
			}

			foreach (var change in finalChangesList)
			{
				// Carrega conteúdo original se existir
				if (string.IsNullOrEmpty(change.OriginalContent) && File.Exists(change.FilePath))
				{
					try
					{
						change.OriginalContent = await _fileSystemService.ReadFileContentAsync(change.FilePath);
					}
					catch
					{
						change.OriginalContent = string.Empty;
					}
				}

				if (!File.Exists(change.FilePath) && change.Status == "Pendente")
				{
					change.Status = "Novo Arquivo";
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
			DetectedChanges.Add(new ProposedFileChange { FilePath = "Erro Fatal", NewContent = ex.ToString(), Status = "Erro" });
		}
		finally
		{
			IsProcessing = false;
		}
	}

	[RelayCommand]
	private async Task ApplySelectedChangesAsync()
	{
		var toApply = DetectedChanges.Where(c => c.IsSelected && c.Status != "Aplicado").ToList();
		if (!toApply.Any()) return;

		IsProcessing = true;
		foreach (var change in toApply)
		{
			try
			{
				var historyEntry = new ModificationHistoryEntry(change.FilePath, change.OriginalContent);
				History.Insert(0, historyEntry);

				await _fileSystemService.SaveFileContentAsync(change.FilePath, change.NewContent);

				change.Status = "Aplicado";
				change.OriginalContent = change.NewContent;
			}
			catch (Exception ex)
			{
				change.Status = $"Erro ao salvar: {ex.Message}";
			}
		}
		IsProcessing = false;
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

			var pendingChange = DetectedChanges.FirstOrDefault(c => c.FilePath == entry.FilePath);
			if (pendingChange != null)
			{
				pendingChange.Status = "Revertido";
				pendingChange.OriginalContent = entry.PreviousContent;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao reverter: {ex.Message}");
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
		SelectedChange = null;
		DiffLines.Clear();
	}

	public async Task GenerateDiffForSelectedAsync()
	{
		if (SelectedChange == null)
		{
			DiffLines.Clear();
			return;
		}

		IsProcessing = true;
		try
		{
			// ALTERAÇÃO: Usando ComputeSemanticDiffAsync em vez de ComputeDiff (texto simples)
			var lines = await _semanticDiffService.ComputeSemanticDiffAsync(
				SelectedChange.OriginalContent ?? "",
				SelectedChange.NewContent ?? ""
			);

			DiffLines.Clear();
			foreach (var line in lines) DiffLines.Add(line);
		}
		finally
		{
			IsProcessing = false;
		}
	}
}