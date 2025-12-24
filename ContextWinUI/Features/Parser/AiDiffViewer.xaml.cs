using ContextWinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ContextWinUI.Views;

public sealed partial class AiDiffViewer : UserControl
{
	// Helper para acessar o ViewModel tipado (agora pegando do DataContext)
	public AiChangesViewModel? ViewModel => this.DataContext as AiChangesViewModel;

	public AiDiffViewer()
	{
		this.InitializeComponent();

		// Escuta mudanças no DataContext para atualizar os x:Bind
		this.DataContextChanged += (s, e) =>
		{
			if (this.Bindings != null)
			{
				this.Bindings.Update();
			}
		};
	}
}