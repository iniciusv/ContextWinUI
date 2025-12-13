using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Services;

namespace ContextWinUI.ViewModels;

public partial class PrePromptViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty]
	private string text = string.Empty;

	[ObservableProperty]
	private bool omitUsings;

	[ObservableProperty]
	private bool omitComments;

	public PrePromptViewModel(IProjectSessionManager sessionManager)
	{
		_sessionManager = sessionManager;
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Sincroniza UI com dados carregados
		Text = _sessionManager.PrePrompt;
		OmitUsings = _sessionManager.OmitUsings;
		OmitComments = _sessionManager.OmitComments;
	}

	// Métodos parciais gerados pelo Toolkit ao alterar as propriedades
	partial void OnTextChanged(string value) => _sessionManager.PrePrompt = value;

	partial void OnOmitUsingsChanged(bool value) => _sessionManager.OmitUsings = value;

	partial void OnOmitCommentsChanged(bool value) => _sessionManager.OmitComments = value;
}