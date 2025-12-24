// ARQUIVO: AiChangesViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Models;
using ContextWinUI.Services;
using Microsoft.CodeAnalysis.Text;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

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
		GraphLines.Clear();
		SelectedChange = null;
		DebugInfo = "Processando input..."; // Feedback imediato

		try
		{
			var projectRoot = _sessionManager.CurrentProjectPath;

			// 1. Indexação (Importante!)
			//StatusMessage = "Indexando grafo...";
			var currentGraph = await _semanticIndexService.GetOrIndexProjectAsync(projectRoot);

			// 2. Parser com Grafo
			var changes = await Task.Run(() => _parser.ParseInput(RawInput, projectRoot, currentGraph));

			foreach (var change in changes)
			{
				if (string.IsNullOrEmpty(change.OriginalContent) && File.Exists(change.FilePath))
				{
					change.OriginalContent = await _fileSystemService.ReadFileContentAsync(change.FilePath);
				}
				DetectedChanges.Add(change);
			}

			if (DetectedChanges.Any())
			{
				// Define a seleção
				var firstChange = DetectedChanges.First();
				SelectedChange = firstChange;

				// --- CORREÇÃO AQUI: Força a chamada explícita ---
				// Não confie apenas no OnSelectedChangeChanged quando definir via código
				DebugInfo = $"Arquivo detectado: {firstChange.FileName}. Gerando visualização...";
				await GenerateGraphVisualizationAsync();
				// ------------------------------------------------
			}
			else
			{
				DebugInfo = "Nenhuma mudança detectada pelo Parser.";
			}
		}
		catch (Exception ex)
		{
			DetectedChanges.Add(new ProposedFileChange { FilePath = "Erro", NewContent = ex.ToString(), Status = "Erro" });
			DebugInfo = $"Erro no processamento: {ex.Message}";
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

	// Adicione esta coleção
	[ObservableProperty]
	private ObservableCollection<GraphCodeLine> graphLines = new();

	[ObservableProperty]
	private string debugInfo = "Aguardando...";

	// Modifique o método que é chamado quando um arquivo é selecionado
	public async Task GenerateGraphVisualizationAsync()
	{
		// Obtém o Dispatcher da thread atual (UI) para garantir atualizações seguras
		var dispatcher = DispatcherQueue.GetForCurrentThread();

		// Atualiza info inicial
		dispatcher.TryEnqueue(() =>
		{
			DebugInfo = "Iniciando leitura para visualização...";
			GraphLines.Clear();
		});

		if (SelectedChange == null) return;

		IsProcessing = true;

		try
		{
			// 1. Garante o conteúdo
			string content = SelectedChange.OriginalContent;

			// Se estiver vazio no objeto, tenta ler do disco novamente
			if (string.IsNullOrEmpty(content) && File.Exists(SelectedChange.FilePath))
			{
				content = await _fileSystemService.ReadFileContentAsync(SelectedChange.FilePath);
				SelectedChange.OriginalContent = content; // Salva para não ler de novo
			}

			// Diagnóstico: Se continuar vazio, avisa
			if (string.IsNullOrEmpty(content))
			{
				dispatcher.TryEnqueue(() =>
				{
					DebugInfo = $"ALERTA: Conteúdo vazio ou arquivo não encontrado em: {SelectedChange.FilePath}";
					IsProcessing = false;
				});
				return;
			}

			// 2. Prepara o Grafo (em background para não travar a UI)
			List<SymbolNode> fileNodes = new();
			await Task.Run(async () =>
			{
				var graph = await _semanticIndexService.GetOrIndexProjectAsync(_sessionManager.CurrentProjectPath);
				var pathKey = SelectedChange.FilePath.ToLowerInvariant();

				if (graph.FileIndex.TryGetValue(pathKey, out var nodes))
				{
					fileNodes = nodes.ToList();
				}
				else
				{
					// Tentativa de fallback por nome de arquivo se o caminho completo falhar
					var partialKey = graph.FileIndex.Keys.FirstOrDefault(k => k.EndsWith(Path.GetFileName(pathKey), StringComparison.OrdinalIgnoreCase));
					if (partialKey != null)
					{
						fileNodes = graph.FileIndex[partialKey].ToList();
					}
				}
			});

			// 3. Processa as linhas (SourceText é rápido, pode ser na thread principal ou background)
			var sourceText = SourceText.From(content);
			var linesToRender = new List<GraphCodeLine>();

			foreach (var line in sourceText.Lines)
			{
				var lineSpan = line.Span;
				var lineText = line.ToString();

				// Acha o nó correspondente
				var matchingNode = fileNodes
					.Where(n => n.StartPosition <= lineSpan.End && (n.StartPosition + n.Length) >= lineSpan.Start)
					.OrderBy(n => n.Length) // Pega o menor nó (mais específico)
					.FirstOrDefault();

				linesToRender.Add(new GraphCodeLine
				{
					LineNumber = line.LineNumber + 1,
					Text = lineText, // Tabs serão renderizados como espaços pelo TextBlock
					SymbolName = matchingNode?.Name ?? "",
					SymbolType = matchingNode?.Type.ToString() ?? "",
					BackgroundColor = GetColorForNodeType(matchingNode?.Type),
					BorderColor = GetBorderColorForNodeType(matchingNode?.Type)
				});
			}

			// 4. ATUALIZAÇÃO DA UI (CRÍTICO: Deve ser na Thread de UI)
			dispatcher.TryEnqueue(() =>
			{
				GraphLines.Clear();
				foreach (var item in linesToRender)
				{
					GraphLines.Add(item);
				}

				// Diagnóstico Final
				DebugInfo = $"Sucesso: {linesToRender.Count} linhas renderizadas. (Nós no grafo: {fileNodes.Count})";
				IsProcessing = false;
			});
		}
		catch (Exception ex)
		{
			dispatcher.TryEnqueue(() =>
			{
				DebugInfo = $"ERRO CRÍTICO: {ex.Message}";
				IsProcessing = false;
			});
		}
	}

	private SolidColorBrush GetColorForNodeType(SymbolType? type)
	{
		if (type == null) return new SolidColorBrush(Colors.Transparent);

		// Cores bem suaves para fundo (Alpha 20-30)
		return type switch
		{
			SymbolType.Method => new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)), // Azulzinho
			SymbolType.Class => new SolidColorBrush(Color.FromArgb(20, 0, 150, 0)),    // Verdinho
			SymbolType.Interface => new SolidColorBrush(Color.FromArgb(20, 150, 0, 150)), // Roxo
			SymbolType.Property => new SolidColorBrush(Color.FromArgb(30, 255, 140, 0)),  // Laranja
			_ => new SolidColorBrush(Colors.Transparent)
		};
	}

	private SolidColorBrush GetBorderColorForNodeType(SymbolType? type)
	{
		if (type == null) return new SolidColorBrush(Colors.Transparent);

		// Cores sólidas para a borda lateral
		return type switch
		{
			SymbolType.Method => new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
			SymbolType.Class => new SolidColorBrush(Color.FromArgb(255, 0, 150, 0)),
			SymbolType.Interface => new SolidColorBrush(Color.FromArgb(255, 150, 0, 150)),
			SymbolType.Property => new SolidColorBrush(Color.FromArgb(255, 255, 140, 0)),
			_ => new SolidColorBrush(Colors.Transparent)
		};
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