using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface ISelectionIOService
{
	Task SaveSelectionAsync(IEnumerable<string> filePaths);
	Task<IEnumerable<string>> LoadSelectionAsync();
}