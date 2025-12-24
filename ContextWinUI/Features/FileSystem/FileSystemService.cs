using ContextWinUI.Core.Contracts;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class FileSystemService : IFileSystemService
{
	private readonly IFileSystemItemFactory _itemFactory;

	private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
	{
		"bin", "obj", ".vs", ".git", ".svn", "node_modules",
		"packages", ".idea", "Debug", "Release", ".vscode"
	};

	public FileSystemService(IFileSystemItemFactory itemFactory)
	{
		_itemFactory = itemFactory;
	}

	public async Task<ObservableCollection<FileSystemItem>> LoadProjectRecursivelyAsync(string rootPath)
	{
		return await Task.Run(() =>
		{
			var items = new ObservableCollection<FileSystemItem>();
			if (!Directory.Exists(rootPath)) return items;

			var rootDir = new DirectoryInfo(rootPath);
			var children = LoadDirectoryInternal(rootDir);

			foreach (var child in children) items.Add(child);
			return items;
		});
	}

	private List<FileSystemItem> LoadDirectoryInternal(DirectoryInfo dir)
	{
		var items = new List<FileSystemItem>();

		try
		{
			var subDirs = dir.GetDirectories()
							 .Where(d => !IgnoredFolders.Contains(d.Name))
							 .OrderBy(d => d.Name);

			foreach (var subDir in subDirs)
			{
				var folderItem = _itemFactory.CreateWrapper(subDir);
				var children = LoadDirectoryInternal(subDir);

				foreach (var child in children) folderItem.Children.Add(child);
				items.Add(folderItem);
			}

			var files = dir.GetFiles().OrderBy(f => f.Name);
			foreach (var file in files)
			{
				items.Add(_itemFactory.CreateWrapper(file));
			}
		}
		catch (UnauthorizedAccessException) { }

		return items;
	}

	public async Task<string> ReadFileContentAsync(string filePath)
	{
		try
		{
			return await File.ReadAllTextAsync(filePath);
		}
		catch (Exception ex)
		{
			return $"Erro ao ler arquivo: {ex.Message}";
		}
	}
	public async Task SaveFileContentAsync(string filePath, string content)
	{
		try
		{
			await File.WriteAllTextAsync(filePath, content);
		}
		catch (Exception ex)
		{
			throw new Exception($"Erro ao salvar arquivo: {ex.Message}");
		}
	}
	public async Task CreateDirectoryAsync(string path)
	{
		await Task.Run(() =>
		{
			if (Directory.Exists(path)) throw new Exception("A pasta já existe.");
			Directory.CreateDirectory(path);
		});
	}

	public async Task CreateFileAsync(string path)
	{
		await Task.Run(() =>
		{
			if (File.Exists(path)) throw new Exception("O arquivo já existe.");
			File.Create(path).Dispose(); // Cria e fecha imediatamente
		});
	}

	public async Task DeleteItemAsync(string path)
	{
		await Task.Run(() =>
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, true); // true = recursivo
			}
			else if (File.Exists(path))
			{
				File.Delete(path);
			}
		});
	}
}