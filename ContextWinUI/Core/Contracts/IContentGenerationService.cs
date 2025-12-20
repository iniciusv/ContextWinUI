using ContextWinUI.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IContentGenerationService
{
	Task<string> GenerateContentAsync(IEnumerable<FileSystemItem> items, IProjectSessionManager sessionSettings);
}