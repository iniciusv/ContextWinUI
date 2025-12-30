using ContextWinUI.Core.Algorithms;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Shared;
using ContextWinUI.Features.CodeAnalyses;
using ContextWinUI.Features.ContextBuilder;
using ContextWinUI.Features.GraphParser.ViewModels;
using ContextWinUI.Models;
using ContextWinUI.Services;
using ContextWinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
// Adicione outros namespaces necessários (onde estão suas implementações como LevenshteinEngine)

namespace ContextWinUI;

public partial class App : Application
{
	public static MainWindow? MainWindow { get; private set; }

	// Propriedade para acessar o container de injeção
	public IServiceProvider Services { get; }

	public App()
	{
		InitializeComponent();
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		// Configura e constrói o container
		Services = ConfigureServices();
	}

	private static IServiceProvider ConfigureServices()
	{
		var services = new ServiceCollection();

		// =========================================================
		// 1. SERVIÇOS DE INFRAESTRUTURA (Singletons = Estado Global/Cache)
		// =========================================================

		// Factory mantém cache de wrappers, precisa ser Singleton
		services.AddSingleton<IFileSystemItemFactory, FileSystemItemFactory>();

		// Serviços IO geralmente podem ser Singleton
		services.AddSingleton<IFileSystemService, FileSystemService>();
		services.AddSingleton<IPersistenceService, PersistenceService>();
		services.AddSingleton<IGitService, GitService>();
		services.AddSingleton<ISelectionIOService, SelectionIOService>();

		// Gerenciamento de UI (Tags)
		services.AddSingleton<ITagManagementUiService, TagManagementUiService>();

		// =========================================================
		// 2. SERVIÇOS DE ESTADO CRÍTICO (Singletons Obrigatórios)
		// =========================================================

		// Mantém qual arquivo está selecionado no momento (Isolado)
		services.AddSingleton<IFileSelectionService, FileSelectionService>();

		// Mantém o estado do projeto (Carregado/Não Carregado, Caminho, Configs)
		services.AddSingleton<IProjectSessionManager, ProjectSessionManager>();

		// Mantém o grafo de dependências pesado em memória
		services.AddSingleton<SemanticIndexService>();

		// =========================================================
		// 3. LÓGICA DE NEGÓCIO E ALGORITMOS (Transient = Criado sob demanda)
		// =========================================================

		// Motores de busca e similaridade (não guardam estado)
		services.AddTransient<ITextSimilarityEngine, LevenshteinEngine>();
		services.AddTransient<DependencyTrackerService>();

		// O Orchestrator coordena os acima. Ele é complexo, mas stateless se as deps forem injetadas.
		services.AddTransient<IDependencyAnalysisOrchestrator, DependencyAnalysisOrchestrator>();

		// =========================================================
		// 4. VIEWMODELS COMPARTILHADOS
		// =========================================================

		// ATENÇÃO: Este ViewModel é passado tanto para o Explorer quanto para o Analysis.
		// Ele DEVE ser Singleton para que os checkboxes marquem nos dois lugares.
		services.AddSingleton<ContextSelectionViewModel>();

		// =========================================================
		// 5. VIEWMODELS PRINCIPAIS (Transient)
		// =========================================================
		// Eles são criados quando o MainViewModel pede. 

		services.AddTransient<FileExplorerViewModel>();
		services.AddTransient<ContextAnalysisViewModel>();
		services.AddTransient<FileContentViewModel>();
		services.AddTransient<PrePromptViewModel>();
		services.AddTransient<GraphParserViewModel>();

		// O Pai de todos
		services.AddTransient<MainViewModel>();

		return services.BuildServiceProvider();
	}

	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		MainWindow = new MainWindow();
		MainWindow.Activate();
	}
}