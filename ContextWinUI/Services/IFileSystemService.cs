using ContextWinUI.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IFileSystemService
{
	Task<ObservableCollection<FileSystemItem>> LoadProjectRecursivelyAsync(string rootPath);
	Task<string> ReadFileContentAsync(string filePath);
}