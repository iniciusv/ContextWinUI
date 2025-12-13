using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();
		ViewModel = new MainViewModel();

		Title = "Context WinUI - Explorador de Código";

		if (Content is FrameworkElement fe)
		{
			fe.Loaded += (s, e) => { this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700)); };
		}

		// Conecta o evento de preview da Análise com o ViewModel de Conteúdo
		ViewModel.ContextAnalysis.FileSelectedForPreview += async (s, item) =>
		{
			await ViewModel.FileContent.LoadFileAsync(item);
		};
	}

	// Tratador de evento vindo do FileExplorerView
	private void OnFileExplorer_FileSelected(object sender, FileSystemItem item)
	{
		ViewModel.OnFileSelected(item);
	}

	// --- CORREÇÃO AQUI ---
	// Usamos o ExecuteAsync do comando gerado pelo RelayCommand
	[RelayCommand]
	private async Task AnalyzeContextAsync()
	{
		await ViewModel.AnalyzeContextCommand.ExecuteAsync(null);
	}
}