using ContextWinUI;
using ContextWinUI.Core.Algorithms;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Features.FileSystem;
using ContextWinUI.Features.GraphAnalysis;
using ContextWinUI.Features.GraphParser;
using ContextWinUI.Features.GraphParser.IAParser;
using ContextWinUI.Features.GraphParser.Interfaces;
using ContextWinUI.Features.GraphParser.Services;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Features.Session; // NEW
using ContextWinUI.Features.DatabaseContext.Services;
using ContextWinUI.Features.DatabaseContext.ViewModels;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using ContextWinUI.ViewModels.Helpers; // Para FileExplorerOperations
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;

namespace ContextWinUI;

public partial class App : Application
{
	// Esta propriedade estática resolve os erros CS0117 nos outros arquivos
	public static Window? MainWindow { get; private set; }

	public IServiceProvider Services { get; }

	public App()
	{
		this.InitializeComponent(); // Agora deve funcionar
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		Services = ConfigureServices();
	}

	private static IServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		// 1. INFRAESTRUTURA
		services.AddSingleton<IFileSystemItemFactory, FileSystemItemFactory>();
		services.AddSingleton<IFileSystemService, FileSystemService>();
		services.AddSingleton<IPersistenceService, PersistenceService>(); // Verifique se essa classe existe no seu projeto
		services.AddSingleton<IGitService, GitService>(); // Verifique se essa classe existe
		services.AddSingleton<ISelectionIOService, SelectionIOService>(); // Verifique se essa classe existe
		services.AddSingleton<ITagManagementUiService, TagManagementUiService>();

		services.AddSingleton<ICodeBlockParserService, CodeBlockParserService>();
		services.AddSingleton<IBlockIdentityService, BlockIdentityService>();

		// 2. ESTADO CRÍTICO
		services.AddSingleton<IFileSelectionService, FileSelectionService>();
		services.AddSingleton<IProjectSessionManager, ProjectSessionManager>();
		services.AddSingleton<ISemanticIndexService, SemanticIndexService>();
		services.AddSingleton<IAiCodeMerger, AiCodeMerger>();

		// 3. LÓGICA
		services.AddSingleton<IBlockSelectionManager, BlockSelectionService>();
        services.AddTransient<IProjectSearchService, ProjectSearchService>(); // NEW
		services.AddTransient<ITextSimilarityEngine, LevenshteinEngine>(); // Verifique se existe
		services.AddTransient<DependencyTrackerService>(); // Verifique se existe
		services.AddTransient<IDependencyAnalysisOrchestrator, DependencyAnalysisOrchestrator>(); // Verifique se existe
		services.AddTransient<ISymbolResolutionService, SymbolResolutionService>();
		services.AddTransient<IVersionDiffManager, VersionDiffManager>();
		services.AddSingleton<IBlockEditorService, BlockEditorService>();
		services.AddTransient<IBlockLoaderService, BlockLoaderService>();
		services.AddSingleton<IFileSegmentsViewModelFactory, FileSegmentsViewModelFactory>();
		services.AddSingleton<IPreviewManager, PreviewManager>();

		// 4. FILE EXPLORER (Refatorado)
		services.AddSingleton<IFileExplorerDataService, FileExplorerDataService>();
		services.AddTransient<IFileExplorerSearchService, FileExplorerSearchService>();
		services.AddTransient<IFileExplorerTreeService, FileExplorerTreeService>();
		services.AddTransient<IFileExplorerOperationsService, FileExplorerOperations>();
        
        services.AddTransient<IGitComparisonService, GitComparisonService>(); // NEW

		// 5. VIEWMODELS
		services.AddSingleton<ContextSelectionViewModel>();

		services.AddTransient<SemanticGraphViewModel>();
		services.AddTransient<FileExplorerViewModel>();
		services.AddTransient<ContextAnalysisViewModel>(); // Verifique se existe
		services.AddTransient<FileContentViewModel>(); // Verifique se existe
		services.AddTransient<PrePromptViewModel>(); // Verifique se existe
		
        // GraphParser MUST be singleton to maintain state across ViewModels
		services.AddSingleton<GraphParserViewModel>(); 
        services.AddSingleton<IGraphParserContract>(sp => sp.GetRequiredService<GraphParserViewModel>());

        // 6. DATABASE FEATURE
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddTransient<DatabaseSelectionViewModel>();
        services.AddTransient<DatabaseSchemaDataViewModel>();
        services.AddTransient<DatabaseExplorerViewModel>();
        services.AddTransient<DatabaseViewModel>();

		services.AddTransient<MainViewModel>();

		return services.BuildServiceProvider();
	}

	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		// Aqui instanciamos a MainWindow e atribuímos à propriedade estática
		var mainWindow = new MainWindow();
		MainWindow = mainWindow;
		mainWindow.Activate();
	}
}