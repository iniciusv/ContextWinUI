using Microsoft.UI.Xaml;

namespace ContextWinUI;

public partial class App : Application
{
	// Propriedade estática para acessar a janela principal
	public static MainWindow? MainWindow { get; private set; }

	public App()
	{
		InitializeComponent();
	}

	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		MainWindow = new MainWindow();
		MainWindow.Activate();
	}
}