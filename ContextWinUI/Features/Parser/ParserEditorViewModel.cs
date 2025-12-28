// ARQUIVO: ParserEditorViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphView;
using ContextWinUI.Features.Parser.Models;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer; // Necessário para Clipboard

namespace ContextWinUI.Features.Parser;

public partial class ParserEditorViewModel : ObservableObject
{
	private readonly SemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private readonly ISnippetFileRelationService _relationService; // <--- NOVO SERVIÇO
	public IAsyncRelayCommand AnalyzeClipboardCommand { get; }
	private readonly DispatcherQueue _dispatcherQueue;

	[ObservableProperty]
	private string codeContent = string.Empty;

	[ObservableProperty]
	private string currentFilePath = string.Empty;

	// Cache do texto que está no clipboard para evitar leituras repetidas
	private string _cachedClipboardText = string.Empty;

	public ObservableCollection<SymbolNode> FileSymbols { get; } = new();
	public ObservableCollection<SymbolNode> ActiveSymbols { get; } = new();

	// Dicionário para buscar rapidamente se um símbolo tem uma refatoração pendente
	// Chave: SymbolNode.Id (do arquivo), Valor: O Match correspondente
	private Dictionary<string, ScopeMatch> _pendingRefactorings = new();

	// Evento para solicitar substituição direta no editor (Low-level)
	public event EventHandler<(int Start, int Length, string Text)>? RequestTextReplacement;

	// Evento para solicitar confirmação do usuário (High-level UI Dialog)
	public event EventHandler<RefactoringSuggestion>? RequestRefactoringConfirmation;

	public ParserEditorViewModel(
		SemanticIndexService indexService,
		IFileSystemService fileSystemService,
		ISnippetFileRelationService relationService) // Injeção do novo serviço
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_relationService = relationService;
		_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
		AnalyzeClipboardCommand = new AsyncRelayCommand(AnalyzeClipboardAsync);
	}

	public async Task AnalyzeClipboardAsync()
	{
		try
		{
			var dataPackageView = Clipboard.GetContent();
			if (!dataPackageView.Contains(StandardDataFormats.Text)) return;

			string clipboardText = await dataPackageView.GetTextAsync();

			// Evita reprocessar se o clipboard não mudou
			if (clipboardText == _cachedClipboardText && _pendingRefactorings.Count > 0) return;

			_cachedClipboardText = clipboardText;
			_pendingRefactorings.Clear();

			// 1. Usa o serviço para comparar o Clipboard (Snippet) com o Arquivo Atual
			var comparisonResult = await _relationService.CompareSnippetWithFileAsync(
				clipboardText,
				CodeContent,
				CurrentFilePath
			);

			// 2. Mapeia os resultados para acesso rápido
			foreach (var match in comparisonResult.ScopeMatches)
			{
				if (match.FileScope != null)
				{
					// Armazena a sugestão vinculada ao ID do símbolo local
					_pendingRefactorings[match.FileScope.Id] = match;

					// Opcional: Aqui você poderia atualizar uma propriedade 'HasPendingChanges' 
					// no SymbolNode visualmente, se sua UI suportar binding direto no node.
				}
			}

			// Notifica a UI que a análise terminou (se necessário para habilitar botões)
			OnPropertyChanged(nameof(FileSymbols));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao analisar clipboard: {ex.Message}");
		}
	}

	public async Task LoadFileAsync(string filePath)
	{
		if (string.IsNullOrEmpty(filePath)) return;

		CurrentFilePath = filePath;
		ActiveSymbols.Clear();

		try
		{
			// 1. Carrega conteúdo bruto
			CodeContent = await _fileSystemService.ReadFileContentAsync(filePath);

			// 2. Garante que o projeto/pasta pai esteja indexado no grafo
			var dir = Path.GetDirectoryName(filePath);
			if (dir != null)
			{
				// Dispara a indexação em background se necessário
				await _indexService.GetOrIndexProjectAsync(dir);
			}
			RefreshFileSymbols();
		}
		catch (Exception ex)
		{
			CodeContent = $"// Erro ao carregar para refatoração: {ex.Message}";
		}
	}

	// Chamado pela View quando o cursor muda de posição
	public void OnCaretMoved(int visualPosition)
	{
		if (string.IsNullOrEmpty(CurrentFilePath)) return;

		// 1. Mapeia a posição visual para física baseada no texto atual
		int physicalPosition = TextIndexMapper.VisualToPhysical(CodeContent, visualPosition);

		// CORREÇÃO: Não use _indexService.GetCurrentGraph().
		// Use a lista local 'FileSymbols' que está sincronizada com o CodeContent atual.

		_dispatcherQueue.TryEnqueue(() =>
		{
			ActiveSymbols.Clear();

			// Busca na lista local de símbolos já parseados
			var matches = FileSymbols
				.Where(s => physicalPosition >= s.StartPosition && physicalPosition <= (s.StartPosition + s.Length))
				.OrderBy(s => s.Length) // Prioriza o escopo mais específico (menor)
				.ToList();

			foreach (var match in matches)
			{
				if (match.Type == SymbolType.Class ||
					match.Type == SymbolType.Method ||
					match.Type == SymbolType.Property ||
					match.Type == SymbolType.Interface ||
					match.Type == SymbolType.Constructor)
				{
					ActiveSymbols.Add(match);
				}
			}
		});
	}

	[RelayCommand]
	private async Task PasteFromClipboardToSymbolAsync(SymbolNode symbol)
	{
		if (symbol == null) return;

		// 1. Verifica se temos uma análise inteligente pronta para este símbolo
		if (_pendingRefactorings.TryGetValue(symbol.Id, out var match))
		{
			// --- CAMINHO INTELIGENTE ---
			// O usuário clicou num método que sabemos que tem uma versão nova no clipboard

			try
			{
				// Extrai apenas o código limpo do método correspondente no snippet
				string newCode = await _relationService.ExtractMatchingSymbolFromSnippetAsync(
					_cachedClipboardText,
					symbol // Passamos o símbolo alvo para guiar a extração
				);

				if (string.IsNullOrEmpty(newCode))
				{
					// Se falhar a extração, fallback para o texto bruto do match
					// (Isso assume que o snippetScope tem os offsets corretos relativos ao snippet)
					// Mas ExtractMatchingSymbolFromSnippetAsync é mais seguro.
					newCode = _cachedClipboardText;
				}

				// Cria o objeto de sugestão para a View exibir o "Diff" ou confirmação
				var suggestion = new RefactoringSuggestion
				{
					TargetSymbol = symbol,
					NewCode = newCode,
					Confidence = match.SimilarityScore,
					MatchTypeDescription = match.MatchType == MatchType.Modification
						? "Atualização Detectada"
						: "Substituição"
				};

				// Dispara o evento para a View abrir o Dialog
				RequestRefactoringConfirmation?.Invoke(this, suggestion);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro na extração inteligente: {ex.Message}");
			}
		}
		else
		{
			// --- CAMINHO LEGADO (Fallback) ---
			// Não há correspondência inteligente. Cola o conteúdo bruto do clipboard.
			// Útil se o usuário copiou apenas um pedaço de código sem estrutura de classe.
			try
			{
				var dataPackageView = Clipboard.GetContent();
				if (dataPackageView.Contains(StandardDataFormats.Text))
				{
					string textToPaste = await dataPackageView.GetTextAsync();
					// Aplica direto (Comportamento antigo)
					RequestTextReplacement?.Invoke(this, (symbol.StartPosition, symbol.Length, textToPaste));
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Erro ao acessar clipboard: {ex.Message}");
			}
		}
	}

	public void ApplyRefactoring(RefactoringSuggestion suggestion)
	{
		if (suggestion == null) return;

		// Executa a substituição física no editor
		RequestTextReplacement?.Invoke(this, (
			suggestion.TargetSymbol.StartPosition,
			suggestion.TargetSymbol.Length,
			suggestion.NewCode
		));

		// Remove a pendência processada
		if (_pendingRefactorings.ContainsKey(suggestion.TargetSymbol.Id))
		{
			_pendingRefactorings.Remove(suggestion.TargetSymbol.Id);
		}
	}

		private void RefreshFileSymbols()
	{
		if (string.IsNullOrEmpty(CodeContent)) return;

		// Captura o texto atual para processar na thread de background
		string textToParse = CodeContent;

		_dispatcherQueue.TryEnqueue(async () =>
		{
			// Executa o parse leve em background para não travar a UI
			var localSymbols = await Task.Run(() =>
			{
				var list = new List<SymbolNode>();
				try
				{
					var tree = CSharpSyntaxTree.ParseText(textToParse);
					var root = tree.GetRoot();

					// Busca Classes
					foreach (var node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
					{
						list.Add(new SymbolNode
						{
							Name = node.Identifier.Text,
							Type = SymbolType.Class,
							StartPosition = node.Span.Start,
							Length = node.Span.Length
						});
					}

					// Busca Métodos
					foreach (var node in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
					{
						list.Add(new SymbolNode
						{
							Name = node.Identifier.Text,
							Type = SymbolType.Method,
							StartPosition = node.Span.Start,
							Length = node.Span.Length
						});
					}

					// Busca Construtores
					foreach (var node in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
					{
						list.Add(new SymbolNode
						{
							Name = node.Identifier.Text,
							Type = SymbolType.Constructor,
							StartPosition = node.Span.Start,
							Length = node.Span.Length
						});
					}
				}
				catch { /* Ignora erros de sintaxe durante a digitação */ }

				return list;
			});

			// Atualiza a UI
			FileSymbols.Clear();
			// Ordena por tamanho descendente (Classes atrás, Métodos na frente)
			foreach (var sym in localSymbols.OrderByDescending(s => s.Length))
			{
				FileSymbols.Add(sym);
			}
		});
	}
}