// ARQUIVO: FileSegmentsView.xaml.cs
using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Features.GraphParser.Views;

public sealed partial class FileSegmentsView : UserControl
{
	// Helper para facilitar o x:Bind
	public FileSegmentsViewModel ViewModel => (FileSegmentsViewModel)this.DataContext;

	public FileSegmentsView()
	{
		this.InitializeComponent();
	}
}