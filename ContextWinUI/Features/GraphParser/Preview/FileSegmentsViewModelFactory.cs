using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Services;

public class FileSegmentsViewModelFactory : IFileSegmentsViewModelFactory
{
	// Dependências de serviço que serão injetadas via DI
	private readonly IFileSystemService _fileSystemService;
	private readonly ICodeBlockParserService _parserService;
	private readonly ISymbolResolutionService _symbolService;
	private readonly IVersionDiffManager _diffManager;
	private readonly IAiCodeMerger _mergerService;
	private readonly IBlockLoaderService _loaderService;
	private readonly IBlockEditorService _editorService;
    private readonly IBlockSelectionManager _selectionManager;
    private readonly IGitComparisonService _gitComparisonService; // NEW

	public FileSegmentsViewModelFactory(
		IFileSystemService fileSystemService,
		ICodeBlockParserService parserService,
		ISymbolResolutionService symbolService,
		IVersionDiffManager diffManager,
		IAiCodeMerger mergerService,
		IBlockLoaderService loaderService,
		IBlockEditorService editorService,
        IBlockSelectionManager selectionManager,
        IGitComparisonService gitComparisonService) // NEW
	{
		_fileSystemService = fileSystemService;
		_parserService = parserService;
		_symbolService = symbolService;
		_diffManager = diffManager;
		_mergerService = mergerService;
		_loaderService = loaderService;
		_editorService = editorService;
        _selectionManager = selectionManager;
        _gitComparisonService = gitComparisonService;
	}

	public FileSegmentsViewModel Create(string filePath)
	{
		// Para um preview, criamos um histórico isolado.
		// O preview mostra o estado atual do arquivo no disco.
		var previewHistory = new ObservableCollection<GlobalVersion>
		{
			new GlobalVersion
			{
				Description = "Preview (Atual)",
				IsOriginal = false,
				Timestamp = DateTime.Now
			}
		};

		// Cria a ViewModel injetando os serviços e o caminho do arquivo
		return new FileSegmentsViewModel(
			filePath,
			_fileSystemService,
			_parserService,
			_symbolService,
			_diffManager,
			previewHistory, // Passamos o histórico isolado
			0,              // Índice inicial
			_mergerService,
			_loaderService,
			_editorService,
            _selectionManager,
            _gitComparisonService // NEW
		);
	}
}
