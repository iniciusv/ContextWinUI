using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
// ARQUIVO: MainWindow.xaml.cs
using Microsoft.Extensions.DependencyInjection; // <--- ADICIONE ISSO

namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		InitializeComponent();

		// --- CORREÇÃO AQUI ---
		// Em vez de 'new MainViewModel()', pegamos do container de serviços do App
		// O cast ((App)Application.Current) é necessário para acessar a propriedade .Services
		ViewModel = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();
		// ---------------------

		Title = "Context WinUI - Explorador de Código";

		// Configura o DataContext para que os Bindings {x:Bind} funcionem corretamente
		if (Content is FrameworkElement fe)
		{
			fe.DataContext = ViewModel; // Isso conecta o XAML ao ViewModel injetado

			fe.Loaded += (s, e) =>
			{
				// Ajuste de tamanho da janela (opcional, mantendo seu código)
				this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700));
			};
		}
	}

	// Este método pode ser necessário se você ainda usa eventos no TreeView em vez de Commands
	private void OnFileExplorer_FileSelected(object sender, FileSystemItem item)
	{
		ViewModel.OnFileSelected(item);
	}

	// DICA: Você provavelmente não precisa deste RelayCommand aqui no Code Behind.
	// No XAML do botão, você pode fazer Command="{x:Bind ViewModel.AnalyzeContextCommand}" direto.
	[RelayCommand]
	private async Task AnalyzeContextAsync() => await ViewModel.AnalyzeContextCommand.ExecuteAsync(null);
}