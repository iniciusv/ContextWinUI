using ContextWinUI.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IFileSystemService
{
	Task<ObservableCollection<FileSystemItem>> LoadProjectRecursivelyAsync(string rootPath);
	Task<string> ReadFileContentAsync(string filePath);
	Task SaveFileContentAsync(string filePath, string content);
}