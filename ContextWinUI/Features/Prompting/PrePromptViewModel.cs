// ARQUIVO: PrePromptViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using ContextWinUI.Core.Contracts;
using ContextWinUI.Services;

namespace ContextWinUI.ViewModels;

public partial class PrePromptViewModel : ObservableObject
{
	private readonly IProjectSessionManager _sessionManager;
	private string _text = string.Empty;

	[ObservableProperty] private bool omitUsings;
	[ObservableProperty] private bool omitNamespaces;
	[ObservableProperty] private bool omitComments;
	[ObservableProperty] private bool omitEmptyLines;
	[ObservableProperty] private bool includeStructure;
	[ObservableProperty] private bool structureOnlyFolders;

	public PrePromptViewModel(IProjectSessionManager sessionManager)
	{
		_sessionManager = sessionManager;

		_sessionManager.ProjectLoaded += (s, e) => RefreshData();
		_sessionManager.ContextRestored += (s, e) => RefreshData();
	}

	// Implementação manual para garantir a sincronia exata
	public string Text
	{
		get => _text;
		set
		{
			if (SetProperty(ref _text, value))
			{
				_sessionManager.PrePrompt = value;
			}
		}
	}

	private void RefreshData()
	{
		// Puxa os dados da memória (SessionManager) para a Tela
		Text = _sessionManager.PrePrompt;

		OmitUsings = _sessionManager.OmitUsings;
		OmitNamespaces = _sessionManager.OmitNamespaces;
		OmitComments = _sessionManager.OmitComments;
		OmitEmptyLines = _sessionManager.OmitEmptyLines;
		IncludeStructure = _sessionManager.IncludeStructure;
		StructureOnlyFolders = _sessionManager.StructureOnlyFolders;
	}

	private void OnProjectLoaded(object? sender, ProjectLoadedEventArgs e)
	{
		// Carrega do SessionManager para a Tela
		Text = _sessionManager.PrePrompt;

		OmitUsings = _sessionManager.OmitUsings;
		OmitNamespaces = _sessionManager.OmitNamespaces;
		OmitComments = _sessionManager.OmitComments;
		OmitEmptyLines = _sessionManager.OmitEmptyLines;
		IncludeStructure = _sessionManager.IncludeStructure;
		StructureOnlyFolders = _sessionManager.StructureOnlyFolders;
	}

	// Os booleanos geralmente funcionam bem com o gerador, mas se quiser garantir
	// que eles também não tenham atraso, pode usar On[Nome]Changed sem parâmetros
	// e pegar a propriedade atual, mas vou manter como estava pois o erro critico era no texto.
	partial void OnOmitUsingsChanged(bool value) => _sessionManager.OmitUsings = value;
	partial void OnOmitNamespacesChanged(bool value) => _sessionManager.OmitNamespaces = value;
	partial void OnOmitCommentsChanged(bool value) => _sessionManager.OmitComments = value;
	partial void OnOmitEmptyLinesChanged(bool value) => _sessionManager.OmitEmptyLines = value;
	partial void OnIncludeStructureChanged(bool value) => _sessionManager.IncludeStructure = value;
	partial void OnStructureOnlyFoldersChanged(bool value) => _sessionManager.StructureOnlyFolders = value;
}