using ContextWinUI.Services;using System;using System.Threading.Tasks;namespace ContextWinUI.Core.Contracts;public interface IProjectSessionManager{
	string PrePrompt { get; set; }
	bool OmitUsings { get; set; }
	bool OmitNamespaces { get; set; }
	bool OmitComments { get; set; }
	bool OmitEmptyLines { get; set; }
	bool IncludeStructure { get; set; }
	bool StructureOnlyFolders { get; set; }

	string? CurrentProjectPath { get; }
	bool IsProjectLoaded { get; }

	event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
	event EventHandler<string>? StatusChanged;

	Task LoadProjectAsync();
	Task OpenProjectAsync(string path);
	Task SaveSessionAsync();
	void CloseProject();}