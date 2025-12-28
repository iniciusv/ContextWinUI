// ARQUIVO: ParserEditorViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
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
	private readonly DispatcherQueue _dispatcherQueue;

	[ObservableProperty]
	private string codeContent = string.Empty;

	[ObservableProperty]
	private string currentFilePath = string.Empty;

	public ObservableCollection<SymbolNode> FileSymbols { get; } = new();

	// Coleção de símbolos sob o cursor (apenas para a lista lateral)
	public ObservableCollection<SymbolNode> ActiveSymbols { get; } = new();

	// Evento para solicitar à View que altere o texto no editor
	public event EventHandler<(int Start, int Length, string Text)>? RequestTextReplacement;


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
	public ParserEditorViewModel(SemanticIndexService indexService, IFileSystemService fileSystemService)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
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

		try
		{
			var dataPackageView = Clipboard.GetContent();
			if (dataPackageView.Contains(StandardDataFormats.Text))
			{
				string textToPaste = await dataPackageView.GetTextAsync();

				// Dispara o evento para a View manipular o Editor Control
				RequestTextReplacement?.Invoke(this, (symbol.StartPosition, symbol.Length, textToPaste));
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao acessar clipboard: {ex.Message}");
		}
	}

}