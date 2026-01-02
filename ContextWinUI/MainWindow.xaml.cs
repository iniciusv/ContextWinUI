using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;
// ARQUIVO: MainWindow.xaml.cs
using Microsoft.Extensions.DependencyInjection; 
namespace ContextWinUI;

public sealed partial class MainWindow : Window
{
	public MainViewModel ViewModel { get; }

	public MainWindow()
	{
		this.InitializeComponent();

		ViewModel = ((App)Application.Current).Services.GetRequiredService<MainViewModel>();

		this.Title = "Context WinUI - Explorador de Código";

		if (this.Content is FrameworkElement fe)
		{
			fe.DataContext = ViewModel;

			fe.Loaded += (s, e) =>
			{

				try { this.AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 700)); } catch { }
			};
		}
	}

	private void OnFileExplorer_FileSelected(object sender, FileSystemItem item)
	{
		ViewModel.FileExplorer.SelectFile(item); // Ajustado para acessar via FileExplorer
	}

}