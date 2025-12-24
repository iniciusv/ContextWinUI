using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ContextWinUI.ViewModels;

namespace ContextWinUI.Views;

public sealed partial class AiControlPanel : UserControl
{
	// Helper para facilitar o acesso (útil se você decidir usar x:Bind no futuro)
	public AiChangesViewModel? ViewModel => DataContext as AiChangesViewModel;

	public AiControlPanel()
	{
		this.InitializeComponent();

		// CORREÇÃO AQUI:
		this.DataContextChanged += (s, e) =>
		{
			// O objeto 'Bindings' é gerado automaticamente pelo compilador XAML.
			// Precisamos verificar se ele existe antes de chamar Update().
			if (Bindings != null)
			{
				Bindings.Update();
			}
		};
	}
}