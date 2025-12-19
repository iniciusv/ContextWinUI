using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IGitService
{
	bool IsGitRepository(string rootPath);

	// Retorna os caminhos absolutos dos arquivos alterados
	Task<IEnumerable<string>> GetModifiedFilesAsync(string rootPath);
}