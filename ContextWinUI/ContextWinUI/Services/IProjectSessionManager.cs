using System;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IProjectSessionManager
{
	// --- Configurações Persistidas ---
	string PrePrompt { get; set; }
	bool OmitUsings { get; set; }
	bool OmitComments { get; set; }

	// --- Estado do Projeto ---
	string? CurrentProjectPath { get; }
	bool IsProjectLoaded { get; }

	// --- Eventos ---
	event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
	event EventHandler<string>? StatusChanged;

	// --- Ações ---
	Task OpenProjectAsync(string path);
	Task SaveSessionAsync();
	void CloseProject();
}