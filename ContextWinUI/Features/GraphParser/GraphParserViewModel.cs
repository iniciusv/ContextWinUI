// ARQUIVO: GraphParserViewModel.cs (modificado)
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels;

public partial class GraphParserViewModel : ObservableObject
{
	private readonly SemanticIndexService _indexService;
	private readonly IFileSystemService _fileSystemService;
	private string _rootPath;

	[ObservableProperty]
	private ObservableCollection<FileSegmentsViewModel> tabs = new();

	[ObservableProperty]
	private FileSegmentsViewModel? selectedTab;

	public bool HasTabs => Tabs.Any();

	partial void OnTabsChanged(ObservableCollection<FileSegmentsViewModel> value)
	{
		OnPropertyChanged(nameof(HasTabs));
	}

	partial void OnSelectedTabChanged(FileSegmentsViewModel? value)
	{
		OnPropertyChanged(nameof(HasTabs));
	}

	// ADICIONE ESTA LINHA:
	[ObservableProperty]
	private ObservableCollection<SearchSuggestion> searchSuggestions = new();

	public GraphParserViewModel(
	SemanticIndexService indexService,
	IFileSystemService fileSystemService,
	IProjectSessionManager sessionManager)
	{
		_indexService = indexService;
		_fileSystemService = fileSystemService;
		_rootPath = sessionManager.CurrentProjectPath ?? string.Empty;

		System.Diagnostics.Debug.WriteLine($"GraphParserViewModel criado. RootPath: {_rootPath}");

		// Verifique se temos um projeto carregado
		if (string.IsNullOrEmpty(_rootPath))
		{
			System.Diagnostics.Debug.WriteLine("AVISO: Nenhum projeto carregado. A busca não funcionará até que um projeto seja aberto.");
		}

		// Subscreva ao evento de projeto carregado
		sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		_rootPath = e.RootPath;
		System.Diagnostics.Debug.WriteLine($"Projeto carregado no GraphParserViewModel: {_rootPath}");

		// Inicializa o grafo em background
		_ = InitializeGraphAsync();
	}

	private async Task InitializeGraphAsync()
	{
		try
		{
			if (!string.IsNullOrEmpty(_rootPath))
			{
				await _indexService.GetOrIndexProjectAsync(_rootPath);
				System.Diagnostics.Debug.WriteLine("Grafo indexado e pronto para busca.");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro ao indexar grafo: {ex.Message}");
		}
	}

	[RelayCommand]
	private void UpdateSearch(string query)
	{
		if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(_rootPath))
		{
			SearchSuggestions.Clear();
			System.Diagnostics.Debug.WriteLine($"Busca ignorada. Query: '{query}', RootPath: '{_rootPath}'");
			return;
		}

		try
		{
			System.Diagnostics.Debug.WriteLine($"Iniciando busca por: '{query}' em: {_rootPath}");

			// Obtenha o grafo atual
			var graph = _indexService.GetCurrentGraph();

			if (graph == null || !graph.Nodes.Any())
			{
				System.Diagnostics.Debug.WriteLine("Grafo vazio ou não disponível. Usando fallback.");
				SearchInDirectoryAsSuggestions(query);
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Grafo obtido. Total de nós: {graph.Nodes.Count}");

			var queryLower = query.ToLowerInvariant();
			var suggestions = new List<SearchSuggestion>();

			// Agrupa nós por arquivo
			var nodesByFile = graph.Nodes
				.Where(node => !string.IsNullOrEmpty(node.Value.FilePath) && File.Exists(node.Value.FilePath))
				.GroupBy(node => node.Value.FilePath);

			System.Diagnostics.Debug.WriteLine($"Arquivos únicos no grafo: {nodesByFile.Count()}");

			foreach (var fileGroup in nodesByFile)
			{
				try
				{
					if (string.IsNullOrEmpty(fileGroup.Key))
					{
						System.Diagnostics.Debug.WriteLine("Arquivo com caminho vazio ignorado.");
						continue;
					}

					var fileName = Path.GetFileName(fileGroup.Key);

					// Debug: Log do arquivo sendo processado
					System.Diagnostics.Debug.WriteLine($"Processando arquivo: {fileName}");

					var matchingNodes = fileGroup
						.Where(node =>
							node.Value.Name.ToLowerInvariant().Contains(queryLower) ||
							node.Value.Type.ToString().ToLowerInvariant().Contains(queryLower) ||
							fileName.ToLowerInvariant().Contains(queryLower))
						.ToList();

					if (matchingNodes.Any())
					{
						string relativePath;

						try
						{
							// Calcula caminho relativo com validação
							if (string.IsNullOrEmpty(_rootPath))
							{
								System.Diagnostics.Debug.WriteLine($"ERRO: _rootPath está vazio ao processar {fileName}");
								continue;
							}

							relativePath = Path.GetRelativePath(_rootPath, fileGroup.Key);
							System.Diagnostics.Debug.WriteLine($"Caminho relativo calculado: {relativePath}");
						}
						catch (ArgumentException ex)
						{
							System.Diagnostics.Debug.WriteLine($"Erro ao calcular caminho relativo para {fileGroup.Key}: {ex.Message}");
							// Fallback: usar caminho completo se relativo falhar
							relativePath = fileGroup.Key;
						}

						var symbolsFound = string.Join(", ",
							matchingNodes.Select(n => $"{n.Value.Type}: {n.Value.Name}").Take(3));

						suggestions.Add(new SearchSuggestion
						{
							Title = fileName,
							Subtitle = $"Encontrado: {symbolsFound}",
							FilePath = relativePath,
							Icon = "\uE943",
							MatchCount = matchingNodes.Count
						});
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Erro ao processar arquivo {fileGroup.Key}: {ex.Message}");
				}
			}

			// Ordena por relevância
			suggestions = suggestions
				.OrderByDescending(s => s.MatchCount)
				.ThenBy(s => s.Title)
				.Take(15)
				.ToList();

			System.Diagnostics.Debug.WriteLine($"Sugestões encontradas: {suggestions.Count}");

			// Atualiza a coleção
			SearchSuggestions.Clear();
			foreach (var suggestion in suggestions)
			{
				SearchSuggestions.Add(suggestion);
			}

			// Se não encontrou nada no grafo, faz busca no diretório
			if (!SearchSuggestions.Any())
			{
				System.Diagnostics.Debug.WriteLine("Nenhuma sugestão encontrada no grafo. Tentando busca por diretório...");
				SearchInDirectoryAsSuggestions(query);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro na busca: {ex.Message}");
			SearchInDirectoryAsSuggestions(query);
		}
	}

	// Método de fallback que preenche SearchSuggestions com base na busca no diretório
	private void SearchInDirectoryAsSuggestions(string query)
	{
		try
		{
			if (string.IsNullOrEmpty(_rootPath))
			{
				System.Diagnostics.Debug.WriteLine("_rootPath está vazio. Não é possível buscar no diretório.");
				SearchSuggestions.Clear();

				// Adicione uma sugestão informativa
				SearchSuggestions.Add(new SearchSuggestion
				{
					Title = "Nenhum projeto carregado",
					Subtitle = "Abra um projeto primeiro para usar a busca",
					FilePath = string.Empty,
					Icon = "\uE897", // Ícone de informação
					MatchCount = 0
				});
				return;
			}

			if (!Directory.Exists(_rootPath))
			{
				System.Diagnostics.Debug.WriteLine($"Diretório não existe: {_rootPath}");
				SearchSuggestions.Clear();
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Buscando no diretório: {_rootPath}");

			var files = Directory.EnumerateFiles(_rootPath, "*.cs", SearchOption.AllDirectories)
				.Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
				.Where(f => Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase))
				.Take(15)
				.Select(f =>
				{
					try
					{
						return new SearchSuggestion
						{
							Title = Path.GetFileName(f),
							Subtitle = "Arquivo encontrado no diretório",
							FilePath = Path.GetRelativePath(_rootPath, f),
							Icon = "\uE943",
							MatchCount = 1
						};
					}
					catch (ArgumentException ex)
					{
						System.Diagnostics.Debug.WriteLine($"Erro ao criar sugestão para {f}: {ex.Message}");
						return null;
					}
				})
				.Where(s => s != null)
				.ToList();

			SearchSuggestions.Clear();
			foreach (var suggestion in files)
			{
				SearchSuggestions.Add(suggestion);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Erro na busca no diretório: {ex.Message}");
			SearchSuggestions.Clear();
		}
	}

	[RelayCommand]
	private void OpenFile(object parameter)
	{
		string? path = null;

		// O parâmetro pode ser tanto uma string quanto um SearchSuggestion
		if (parameter is SearchSuggestion suggestion)
		{
			path = suggestion.FilePath;
		}
		else if (parameter is string filePathString)
		{
			path = filePathString;
		}
		else if (parameter is null)
		{
			return;
		}

		if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(_rootPath))
			return;

		string fullPath = Path.Combine(_rootPath, path);
		var existingTab = Tabs.FirstOrDefault(t => t.FilePath == fullPath);

		if (existingTab != null)
		{
			SelectedTab = existingTab;
			return;
		}

		var newTab = new FileSegmentsViewModel(fullPath, _fileSystemService);
		Tabs.Add(newTab);
		SelectedTab = newTab;
	}

	[RelayCommand]
	private void CloseTab(FileSegmentsViewModel tab)
	{
		if (Tabs.Contains(tab))
		{
			Tabs.Remove(tab);
		}
	}
}