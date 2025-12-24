using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels;

public partial class AiChangesViewModel : ObservableObject
{
	// Dependências injetadas
	private readonly IFileSystemService _fileSystemService;
	private readonly IProjectSessionManager _sessionManager;
	private readonly SemanticIndexService _semanticIndexService;
	// Parser interno (inicializado aqui para evitar NullReferenceException)
	private readonly AiResponseParser _parser = new();
	// Propriedades observáveis para a View
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

	private readonly TextDiffService _textDiffService = new(); // Use o novo serviço
	/// <summary>
	/// Construtor recebendo as dependências necessárias, incluindo o SemanticIndexService para o Roslyn.
	/// </summary>
	public AiChangesViewModel(IFileSystemService fileSystemService, IProjectSessionManager sessionManager, SemanticIndexService semanticIndexService)
	{
		_fileSystemService = fileSystemService;
		_sessionManager = sessionManager;
		_semanticIndexService = semanticIndexService;
	}

	/// <summary>
	/// Processa o texto colado para identificar arquivos e conteúdo.
	/// </summary>
	[RelayCommand]
	private async Task ProcessInputAsync()
	{
		// 1. Validações Básicas
		if (string.IsNullOrWhiteSpace(RawInput) || string.IsNullOrEmpty(_sessionManager.CurrentProjectPath))
			return;
		IsProcessing = true;
		DetectedChanges.Clear();
		SelectedChange = null;
		try
		{
			var projectRoot = _sessionManager.CurrentProjectPath;
			var currentGraph = _semanticIndexService.GetCurrentGraph();
			// Variável local para acumular as mudanças antes de jogar na UI
			var finalChangesList = new List<ProposedFileChange>();
			// =================================================================================
			// PASSO 1: Tentativa Padrão (Regex + Identificação de Arquivo Inteiro)
			// =================================================================================
			var standardChanges = await Task.Run(() => _parser.ParseInput(RawInput, projectRoot, currentGraph));
			finalChangesList.AddRange(standardChanges);
			// =================================================================================
			// PASSO 2: Detecção de Snippet / Smart Patching
			// =================================================================================
			// Verifica se o texto tem indícios de ser um snippet parcial (ex: "// ...")
			// ou se o parser padrão não encontrou nada (ex: copiou só um método sem classe)
			bool hasSnippetMarkers = RawInput.Contains("// ...") || RawInput.Contains("//...");
			bool noResultsFound = !finalChangesList.Any();
			bool potentialDestructiveOverwrite = finalChangesList.Any(c => c.Status == "Novo Arquivo" && hasSnippetMarkers);
			if (hasSnippetMarkers || noResultsFound || potentialDestructiveOverwrite)
			{
				// Instancia o Patcher (que usa o código do SmartSnippetPatcher.cs que criamos)
				var patcher = new SmartSnippetPatcher(_semanticIndexService);
				//string relatorioDebug = patcher.InspectSnippetStructure(RawInput);
				// Tenta realizar o merge inteligente
				var smartChange = await patcher.PatchAsync(RawInput, projectRoot);
				if (smartChange != null)
				{
					// Se o Smart Patcher funcionou, ele tem prioridade sobre a detecção padrão
					// porque ele preserva o código original omitido por "// ..."
					// Removemos qualquer detecção "burra" do mesmo arquivo para evitar duplicidade
					finalChangesList.RemoveAll(c => c.FilePath == smartChange.FilePath);
					// Adiciona a versão "mergeada"
					finalChangesList.Add(smartChange);
				}
			}

			// =================================================================================
			// PASSO 3: Carregamento de Conteúdo e Exibição
			// =================================================================================
			foreach (var change in finalChangesList)
			{
				// Se o conteúdo original ainda não foi carregado (e não for um arquivo novo/patch já processado)
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

				// Atualiza o status se for um arquivo novo real
				if (!File.Exists(change.FilePath) && change.Status == "Pendente")
				{
					change.Status = "Novo Arquivo";
				}

				DetectedChanges.Add(change);
			}

			// Seleciona o primeiro item para visualização imediata
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

	/// <summary>
	/// Aplica as alterações marcadas (Checked) no disco.
	/// </summary>
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
				// --- LÓGICA DE HISTÓRICO ADICIONADA AQUI ---
				// Antes de salvar, criamos o registro de histórico com o OriginalContent
				// O OriginalContent é exatamente o que estava no disco antes da IA tocar nele.
				var historyEntry = new ModificationHistoryEntry(change.FilePath, change.OriginalContent);

				// Inserimos no topo (índice 0) para o mais recente aparecer primeiro
				History.Insert(0, historyEntry);
				// ---------------------------------------------

				await _fileSystemService.SaveFileContentAsync(change.FilePath, change.NewContent);

				change.Status = "Aplicado";
				// Atualiza o OriginalContent para o novo, caso o usuário queira aplicar outra mudança em cima
				change.OriginalContent = change.NewContent;
			}
			catch (Exception ex)
			{
				change.Status = $"Erro ao salvar: {ex.Message}";
			}
		}
		IsProcessing = false;
	}

	// 2. NOVO COMANDO: Reverter a alteração
	[RelayCommand]
	private async Task RevertChangeAsync(ModificationHistoryEntry entry)
	{
		if (entry == null) return;

		IsProcessing = true;
		try
		{
			// Salva o conteúdo antigo (PreviousContent) de volta no arquivo
			await _fileSystemService.SaveFileContentAsync(entry.FilePath, entry.PreviousContent);

			// Opcional: Remover do histórico após reverter, ou marcar como revertido.
			// Aqui vou remover para simplificar (se reverteu, saiu da pilha de ações)
			History.Remove(entry);

			// Feedback visual (pode usar o StatusMessage do MainViewModel se tiver acesso, ou um diálogo)
			System.Diagnostics.Debug.WriteLine($"Arquivo {entry.FileName} revertido com sucesso.");

			// Se o arquivo estiver na lista de DetectedChanges, podemos atualizar o status dele também
			var pendingChange = DetectedChanges.FirstOrDefault(c => c.FilePath == entry.FilePath);
			if (pendingChange != null)
			{
				pendingChange.Status = "Revertido";
				pendingChange.OriginalContent = entry.PreviousContent;
				// Nota: O NewContent do pendingChange continua sendo a sugestão da IA, 
				// caso você queira aplicar de novo.
			}
		}
		catch (Exception ex)
		{
			// Tratar erro
			System.Diagnostics.Debug.WriteLine($"Erro ao reverter: {ex.Message}");
		}
		finally
		{
			IsProcessing = false;
		}
	}

	/// <summary>
	/// Limpa a tela para uma nova operação.
	/// </summary>
	[RelayCommand]
	private void Clear()
	{
		RawInput = string.Empty;
		DetectedChanges.Clear();
		SelectedChange = null;
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
			var lines = await Task.Run(() => _textDiffService.ComputeDiff(
				SelectedChange.OriginalContent ?? "",
				SelectedChange.NewContent ?? ""
			));

			DiffLines.Clear();
			foreach (var line in lines) DiffLines.Add(line);
		}
		finally
		{
			IsProcessing = false;
		}
	}
}