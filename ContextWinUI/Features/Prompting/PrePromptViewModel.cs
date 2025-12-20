using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Features.Session;
using ContextWinUI.Services; // Para ProjectLoadedEventArgs

namespace ContextWinUI.ViewModels;

public partial class PrePromptViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;

	// Propriedades ligadas à UI (TextBox e CheckBoxes)
	[ObservableProperty] private string text = string.Empty;
	[ObservableProperty] private bool omitUsings;
	[ObservableProperty] private bool omitNamespaces;
	[ObservableProperty] private bool omitComments;
	[ObservableProperty] private bool omitEmptyLines;
	[ObservableProperty] private bool includeStructure;
	[ObservableProperty] private bool structureOnlyFolders;

	public PrePromptViewModel(IProjectSessionManager sessionManager)
	{
		_sessionManager = sessionManager;

		// --- CRUCIAL: Escutar quando um projeto termina de carregar ---
		_sessionManager.ProjectLoaded += OnProjectLoaded;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Puxa os dados do SessionManager para a Tela
		Text = _sessionManager.PrePrompt ?? string.Empty;
		OmitUsings = _sessionManager.OmitUsings;
		OmitNamespaces = _sessionManager.OmitNamespaces;
		OmitComments = _sessionManager.OmitComments;
		OmitEmptyLines = _sessionManager.OmitEmptyLines;
		IncludeStructure = _sessionManager.IncludeStructure;
		StructureOnlyFolders = _sessionManager.StructureOnlyFolders;
	}

	// Quando o usuário digita na tela, atualizamos o SessionManager
	partial void OnTextChanged(string value) => _sessionManager.PrePrompt = value;
	partial void OnOmitUsingsChanged(bool value) => _sessionManager.OmitUsings = value;
	partial void OnOmitNamespacesChanged(bool value) => _sessionManager.OmitNamespaces = value;
	partial void OnOmitCommentsChanged(bool value) => _sessionManager.OmitComments = value;
	partial void OnOmitEmptyLinesChanged(bool value) => _sessionManager.OmitEmptyLines = value;
	partial void OnIncludeStructureChanged(bool value) => _sessionManager.IncludeStructure = value;
	partial void OnStructureOnlyFoldersChanged(bool value) => _sessionManager.StructureOnlyFolders = value;
}