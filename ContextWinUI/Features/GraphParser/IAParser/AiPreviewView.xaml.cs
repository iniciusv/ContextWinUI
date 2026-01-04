using ContextWinUI.Features.GraphParser.IAParser;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// Note que não precisamos de 'using ...IAParser' aqui pois já estamos DENTRO dele

namespace ContextWinUI.Features.GraphParser; // <--- Namespace atualizado

public sealed partial class AiPreviewView : UserControl
{
	public AiPreviewView()
	{
		this.InitializeComponent(); // Agora vai funcionar
		this.Loaded += OnLoaded;
	}

	public AiPreviewViewModel ViewModel => (AiPreviewViewModel)DataContext;

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (ViewModel != null)
		{
			// Bindings é gerado automaticamente quando o x:Class bate com o namespace
			this.Bindings.Update();
		}
	}
}