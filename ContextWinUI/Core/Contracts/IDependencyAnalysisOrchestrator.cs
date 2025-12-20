using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IDependencyAnalysisOrchestrator
{
	Task EnrichFileNodeAsync(FileSystemItem item, string projectPath);
	Task EnrichMethodFlowAsync(FileSystemItem item, string projectPath);
	Task<string> BuildContextStringAsync(IEnumerable<FileSystemItem> selectedItems, IProjectSessionManager sessionSettings);
}