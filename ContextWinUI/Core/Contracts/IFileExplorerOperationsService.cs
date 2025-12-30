using ContextWinUI.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IFileExplorerOperationsService
{
	event EventHandler<string>? StatusChanged;
	event EventHandler<FileSystemItem>? ItemCreated;
	Task CreateNewItemAsync(FileSystemItem targetItem, bool isFolder, XamlRoot xamlRoot);
	Task DeleteItemAsync(FileSystemItem itemToDelete, ObservableCollection<FileSystemItem> rootItems, XamlRoot xamlRoot);
}