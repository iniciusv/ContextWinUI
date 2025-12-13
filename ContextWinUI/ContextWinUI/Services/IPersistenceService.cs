using ContextWinUI.ContextWinUI.Models;
using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IPersistenceService
{
	/// <summary>
	/// Salva o estado atual (Tags, etc) de todos os itens carregados para um arquivo JSON local.
	/// </summary>
	Task SaveProjectCacheAsync(string projectRootPath, IEnumerable<FileSharedState> states);

	/// <summary>
	/// Tenta carregar o cache salvo anteriormente para este projeto.
	/// </summary>
	Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath);
}