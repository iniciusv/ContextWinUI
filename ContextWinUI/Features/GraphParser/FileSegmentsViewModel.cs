using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.GraphParser.Models;
using Microsoft.UI.Xaml.Media; // Para SolidColorBrush, se necessário aqui ou no Model
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.ViewModels
{
	public partial class FileSegmentsViewModel : ObservableObject
	{
		// Dependências
		private readonly IFileSystemService _fileSystemService;
		private readonly ISemanticIndexService _indexService;
		private readonly ICodeBlockParserService _parserService; // Nova dependência

		// Propriedades Públicas de Leitura
		public string FilePath { get; }
		public string FileName { get; }

		// Coleções Observáveis
		[ObservableProperty]
		private ObservableCollection<CodeBlockItem> blocks = new();

		[ObservableProperty]
		private ObservableCollection<GlobalVersion> globalHistory = new();

		// Estado da Seleção e UI
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasSelectedBlock))]
		private CodeBlockItem? selectedBlock;

		[ObservableProperty]
		private bool isLoading;

		[ObservableProperty]
		private bool isEmpty;

		[ObservableProperty]
		private int currentGlobalIndex = 0;

		[ObservableProperty]
		private string currentSymbolInfo = string.Empty;

		// Propriedades Computadas
		public bool HasSelectedBlock => SelectedBlock != null;
		public bool HasAnyUnsavedChanges => Blocks.Any(b => b.HasUnsavedChanges);

		// Eventos
		public event EventHandler? GlobalSaveRequested;
		public event EventHandler<int>? GlobalRestoreRequested;

		// Construtor
		public FileSegmentsViewModel(
			string filePath,
			IFileSystemService fileSystemService,
			SemanticIndexService indexService,
			ICodeBlockParserService parserService)
		{
			FilePath = filePath;
			FileName = Path.GetFileName(filePath);
			_fileSystemService = fileSystemService;
			_indexService = indexService;
			_parserService = parserService;

			_ = LoadBlocksAsync();
		}

		// ==========================================================
		// LÓGICA DE CARREGAMENTO (Refatorada)
		// ==========================================================

		private async Task LoadBlocksAsync()
		{
			IsLoading = true;
			Blocks.Clear();
			SelectedBlock = null;

			try
			{
				// 1. Ler o conteúdo bruto do arquivo
				var fileContent = await _fileSystemService.ReadFileContentAsync(FilePath);
				if (string.IsNullOrEmpty(fileContent)) return;

				// 2. Usar o serviço para fazer o Parse (Roslyn)
				var parsedItems = await _parserService.ParseFileAsync(FilePath, fileContent);

				// 3. Popular a ViewModel
				foreach (var item in parsedItems)
				{
					Blocks.Add(item);
				}

				if (Blocks.Any())
				{
					SelectedBlock = Blocks.First();
				}

				// 4. Inicializar o histórico de versões
				InitializeGlobalHistory();
			}
			catch (Exception ex)
			{
				// Fallback em caso de erro crítico
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

		// ==========================================================
		// GERENCIAMENTO DE HISTÓRICO E VERSÕES
		// ==========================================================

		public void NotifyUnsavedChanges() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));
		public void NotifyChangesChanged() => OnPropertyChanged(nameof(HasAnyUnsavedChanges));

		private void InitializeGlobalHistory()
		{
			GlobalHistory.Clear();
			GlobalHistory.Add(new GlobalVersion
			{
				Description = "Versão Original",
				IsOriginal = true,
				Timestamp = DateTime.MinValue
			});
			CurrentGlobalIndex = 0;
		}

		public void CommitGlobalVersion(string description = "Salvo em lote")
		{
			var newVersion = new GlobalVersion
			{
				Description = description,
				Timestamp = DateTime.Now,
				IsOriginal = false
			};

			GlobalHistory.Add(newVersion);

			// Cria uma nova versão para cada bloco que foi modificado
			foreach (var block in Blocks)
			{
				if (block.HasUnsavedChanges)
				{
					block.CreateNewVersion(block.Content, description);
				}
			}

			CurrentGlobalIndex = GlobalHistory.Count - 1;
			OnPropertyChanged(nameof(HasAnyUnsavedChanges));
		}

		// Trigger disparado quando a propriedade CurrentGlobalIndex muda na View
		partial void OnCurrentGlobalIndexChanged(int value)
		{
			if (GlobalHistory == null || value < 0 || value >= GlobalHistory.Count) return;
			RestoreGlobalState(value);
		}

		private void RestoreGlobalState(int globalIndex)
		{
			foreach (var block in Blocks)
			{
				// Tenta restaurar a versão correspondente ao índice global.
				// Se o bloco tiver menos versões que o global, pega a última disponível.
				int targetBlockIndex = Math.Min(globalIndex, block.Versions.Count - 1);

				if (targetBlockIndex >= 0)
				{
					block.RestoreVersion(targetBlockIndex);
				}
			}
		}

		// Wrapper methods para disparar eventos para a View/Pai
		public void TriggerGlobalSave() => GlobalSaveRequested?.Invoke(this, EventArgs.Empty);
		public void TriggerGlobalRestore(int index) => GlobalRestoreRequested?.Invoke(this, index);

		// ==========================================================
		// LÓGICA DE RESOLUÇÃO DE SÍMBOLOS (Semantic Index)
		// ==========================================================

		public void ResolveSymbolHeuristic(int cursorIndexInBlock)
		{
			if (SelectedBlock == null) return;

			string word = GetWordAtCursor(SelectedBlock.Content, cursorIndexInBlock);

			// Calcula posição absoluta para consultar o grafo
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

			// Retrocede para achar o início da palavra
			while (start > 0 && IsIdentifierChar(text[start - 1])) start--;

			// Avança para achar o fim da palavra
			while (end < text.Length && IsIdentifierChar(text[end])) end++;

			return text.Substring(start, end - start);
		}

		private bool IsIdentifierChar(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_';
		}
	}
}