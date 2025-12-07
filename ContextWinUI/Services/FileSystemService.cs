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
	// Pastas a ignorar
	private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
	{
		"bin", "obj", ".vs", ".git", ".svn", "node_modules",
		"packages", ".idea", "Debug", "Release"
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
				foreach (var dir in rootDir.GetDirectories().OrderBy(d => d.Name))
				{
					if (!IgnoredFolders.Contains(dir.Name))
					{
						items.Add(CreateFileSystemItem(dir));
					}
				}

				// Depois adiciona arquivos
				foreach (var file in rootDir.GetFiles().OrderBy(f => f.Name))
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
			IsDirectory = info is DirectoryInfo
		};

		if (!item.IsDirectory && info is FileInfo fileInfo)
		{
			item.FileSize = fileInfo.Length;
		}

		if (item.IsDirectory)
		{
			// Carrega filhos lazy (apenas quando expandir)
			item.Children = new ObservableCollection<FileSystemItem>();
		}

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
				foreach (var subDir in dir.GetDirectories().OrderBy(d => d.Name))
				{
					if (!IgnoredFolders.Contains(subDir.Name))
					{
						items.Add(CreateFileSystemItem(subDir));
					}
				}

				// Arquivos depois
				foreach (var file in dir.GetFiles().OrderBy(f => f.Name))
				{
					items.Add(CreateFileSystemItem(file));
				}
			}
			catch (UnauthorizedAccessException)
			{
				// Ignora
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