using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphView; // Certifique-se de que este namespace contém seus Walkers e Strategies
using ContextWinUI.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.ViewModels
{
	public partial class GraphVisualizationViewModel : ObservableObject
	{
		private readonly MainViewModel _mainViewModel;
		private readonly SemanticIndexService _indexService;

		[ObservableProperty]
		private string fileContent = string.Empty;

		[ObservableProperty]
		private IHighlighterStrategy? strategy;

		[ObservableProperty]
		private string currentExtension = ".txt";

		// Propriedades para os Toggles da View
		[ObservableProperty]
		private bool showScopes = true;

		[ObservableProperty]
		private bool showTokens = true;

		public GraphVisualizationViewModel(MainViewModel mainViewModel, SemanticIndexService indexService)
		{
			_mainViewModel = mainViewModel;
			_indexService = indexService;

			// Escuta mudanças na seleção de arquivo principal
			_mainViewModel.FileContent.PropertyChanged += FileContent_PropertyChanged;
		}

		private void FileContent_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FileContentViewModel.FileContent) ||
				e.PropertyName == nameof(FileContentViewModel.SelectedItem))
			{
				UpdateVisualization();
			}
		}

		// Recarrega a visualização se o usuário alterar os checkboxes
		partial void OnShowScopesChanged(bool value) => UpdateVisualization();
		partial void OnShowTokensChanged(bool value) => UpdateVisualization();

		public async void UpdateVisualization()
		{
			var item = _mainViewModel.FileContent.SelectedItem;
			// Pega o conteúdo atualizado da memória
			FileContent = _mainViewModel.FileContent.FileContent;

			// Captura o Dispatcher da thread de UI antes de entrar na Task
			var dispatcher = DispatcherQueue.GetForCurrentThread();

			if (item != null && !string.IsNullOrEmpty(FileContent) && dispatcher != null)
			{
				CurrentExtension = item.SharedState.Extension;
				var fullPath = item.FullPath;

				// Captura configurações para thread-safety
				bool showScopesConfig = ShowScopes;
				bool showTokensConfig = ShowTokens;

				await Task.Run(() =>
				{
					try
					{
						var tree = CSharpSyntaxTree.ParseText(FileContent);
						var root = tree.GetRoot();

						// 1. Coleta a Estrutura Hierárquica (Classes, Métodos, Ifs)
						var scopeWalker = new ScopeGraphBuilderWalker(fullPath);
						scopeWalker.Visit(root);

						// CORREÇÃO AQUI:
						// O Walker agora expõe 'AllNodes' (lista plana) ou 'RootNodes' (hierarquia).
						// Para a estratégia de pintura de fundo, usamos a lista plana 'AllNodes'.
						var scopes = scopeWalker.AllNodes;

						// 2. Coleta os Tokens Granulares (Palavras-chave, Variáveis, Literais)
						// Fazemos isso separadamente para ter granularidade máxima na cor da letra
						var tokens = new List<SymbolNode>();

						foreach (var token in root.DescendantTokens())
						{
							var kind = token.Kind();
							SymbolType type = SymbolType.Statement;

							// Mapeamento simples de sintaxe para SymbolType
							if (token.IsKeyword()) type = SymbolType.Keyword;
							else if (kind == SyntaxKind.IdentifierToken) type = SymbolType.LocalVariable;
							else if (kind == SyntaxKind.StringLiteralToken) type = SymbolType.StringLiteral;
							else if (kind == SyntaxKind.NumericLiteralToken) type = SymbolType.NumericLiteral;
							else if (kind == SyntaxKind.Parameter) type = SymbolType.Parameter;

							// Nota: Tokens geralmente são "folhas", não têm filhos na análise léxica simples,
							// mas podemos tentar vincular ao escopo pai se quisermos (opcional aqui, pois a estratégia já faz o match visual).

							tokens.Add(new SymbolNode
							{
								StartPosition = token.SpanStart,
								Length = token.Span.Length,
								Type = type,
								Name = token.Text,
								FilePath = fullPath
								// Parent será nulo aqui, mas a Estratégia vai cruzar com os scopes para o Tooltip
							});
						}

						// 3. Instancia a estratégia com as duas listas
						var strategy = new LayeredHighlighterStrategy(
							scopes,
							tokens,
							showScopesConfig,
							showTokensConfig
						);

						// 4. Retorna para a UI Thread para atualizar a tela
						dispatcher.TryEnqueue(() =>
						{
							Strategy = strategy;
						});
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine($"Erro ao gerar grafo visual: {ex.Message}");
					}
				});
			}
			else
			{
				Strategy = null;
			}
		}
	}
}