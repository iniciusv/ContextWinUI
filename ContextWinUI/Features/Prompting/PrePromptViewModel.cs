using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Services;

namespace ContextWinUI.ViewModels;

public partial class PrePromptViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;

	[ObservableProperty] private string text = string.Empty;
	[ObservableProperty] private bool omitUsings;
	[ObservableProperty] private bool omitComments;

	// --- NOVAS PROPRIEDADES ---
	[ObservableProperty] private bool includeStructure;
	[ObservableProperty] private bool structureOnlyFolders;

	public PrePromptViewModel(IProjectSessionManager sessionManager)
	{
		_sessionManager = sessionManager;
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		Text = _sessionManager.PrePrompt;
		OmitUsings = _sessionManager.OmitUsings;
		OmitComments = _sessionManager.OmitComments;
		IncludeStructure = _sessionManager.IncludeStructure;
		StructureOnlyFolders = _sessionManager.StructureOnlyFolders;
	}

	partial void OnTextChanged(string value) => _sessionManager.PrePrompt = value;
	partial void OnOmitUsingsChanged(bool value) => _sessionManager.OmitUsings = value;
	partial void OnOmitCommentsChanged(bool value) => _sessionManager.OmitComments = value;
	partial void OnIncludeStructureChanged(bool value) => _sessionManager.IncludeStructure = value;
	partial void OnStructureOnlyFoldersChanged(bool value) => _sessionManager.StructureOnlyFolders = value;
}