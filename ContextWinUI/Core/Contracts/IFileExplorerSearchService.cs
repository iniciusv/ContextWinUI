// ARQUIVO: IFileExplorerSearchService.cs
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Contracts;

public interface IFileExplorerSearchService : IDisposable
{
	Task PerformSearchAsync(IEnumerable<FileSystemItem> items, string query, Action<string> onError);
	Task<string> SubmitSearchAsync(IEnumerable<FileSystemItem> items, string query);
}
