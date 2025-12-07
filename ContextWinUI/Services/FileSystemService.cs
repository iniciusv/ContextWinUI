using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class FileSystemService
{
	private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
	{
		"bin", "obj", ".vs", ".git", ".svn", "node_modules",
		"packages", ".idea", "Debug", "Release", ".vscode"
	};

	public async Task<ObservableCollection<FileSystemItem>> LoadDirectoryAsync(string rootPath)
	{
		return await Task.Run(() =>
		{
			var items = new ObservableCollection<FileSystemItem>();

			if (!Directory.Exists(rootPath))
				return items;

			var rootDir = new DirectoryInfo(rootPath);

			try
			{
				// Primeiro adiciona diretórios
				var directories = rootDir.GetDirectories()
					.Where(d => !IgnoredFolders.Contains(d.Name))
					.OrderBy(d => d.Name);

				foreach (var dir in directories)
				{
					items.Add(CreateFileSystemItem(dir));
				}

				// Depois adiciona arquivos
				var files = rootDir.GetFiles()
					.OrderBy(f => f.Name);

				foreach (var file in files)
				{
					items.Add(CreateFileSystemItem(file));
				}
			}
			catch (UnauthorizedAccessException)
			{
				// Ignora pastas sem permissão
			}

			return items;
		});
	}

	private FileSystemItem CreateFileSystemItem(FileSystemInfo info)
	{
		var item = new FileSystemItem
		{
			Name = info.Name,
			FullPath = info.FullName,
			IsDirectory = info is DirectoryInfo,
			IsExpanded = false
		};

		if (!item.IsDirectory && info is FileInfo fileInfo)
		{
			item.FileSize = fileInfo.Length;
		}

		// Inicializa Children como vazio (será carregado quando expandir)
		item.Children = new ObservableCollection<FileSystemItem>();

		return item;
	}

	public async Task<ObservableCollection<FileSystemItem>> LoadChildrenAsync(FileSystemItem parent)
	{
		if (!parent.IsDirectory)
			return new ObservableCollection<FileSystemItem>();

		return await Task.Run(() =>
		{
			var items = new ObservableCollection<FileSystemItem>();

			try
			{
				var dir = new DirectoryInfo(parent.FullPath);

				// Diretórios primeiro
				var directories = dir.GetDirectories()
					.Where(d => !IgnoredFolders.Contains(d.Name))
					.OrderBy(d => d.Name);

				foreach (var subDir in directories)
				{
					items.Add(CreateFileSystemItem(subDir));
				}

				// Arquivos depois
				var files = dir.GetFiles()
					.OrderBy(f => f.Name);

				foreach (var file in files)
				{
					items.Add(CreateFileSystemItem(file));
				}
			}
			catch (UnauthorizedAccessException)
			{
				// Ignora pastas sem permissão
			}

			return items;
		});
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
}