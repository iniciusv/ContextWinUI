using ContextWinUI.Models;
using System.Threading.Tasks;

namespace ContextWinUI.Services
{
    public interface IStorageFormatHandler
    {
        Task SaveAsync(string filePath, ProjectCacheDto data);
        Task<ProjectCacheDto?> LoadAsync(string filePath);
    }
}
