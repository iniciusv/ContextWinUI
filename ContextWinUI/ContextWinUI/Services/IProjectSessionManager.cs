using System;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IProjectSessionManager
{
	// Propriedades de Estado
	string? CurrentProjectPath { get; }
	bool IsProjectLoaded { get; }

	// Eventos para notificar os ViewModels
	event EventHandler<ProjectLoadedEventArgs>? ProjectLoaded;
	event EventHandler<string>? StatusChanged; // Para atualizar a barra de status

	// Ações
	Task OpenProjectAsync(string path);
	Task SaveSessionAsync();
	void CloseProject();
}