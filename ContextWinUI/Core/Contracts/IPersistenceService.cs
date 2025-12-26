using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IPersistenceService
{
	// Salva no caminho padrão (AppData) calculado via Hash
	Task SaveProjectCacheDefaultAsync(
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders,
		Dictionary<string, string> tagColors);

	// Salva em um arquivo específico escolhido pelo usuário
	Task SaveProjectCacheToSpecificFileAsync(
		string targetFilePath,
		string projectRootPath,
		IEnumerable<FileSharedState> states,
		string prePrompt,
		bool omitUsings,
		bool omitNamespaces,
		bool omitComments,
		bool omitEmptyLines,
		bool includeStructure,
		bool structureOnlyFolders,
		Dictionary<string, string> tagColors);

	// Carrega do caminho padrão
	Task<ProjectCacheDto?> LoadProjectCacheDefaultAsync(string projectRootPath);

	// Carrega de um arquivo específico
	Task<ProjectCacheDto?> LoadProjectCacheFromSpecificFileAsync(string sourceFilePath);
}