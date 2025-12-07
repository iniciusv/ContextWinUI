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

	// Alterado para carregar tudo de uma vez
	public async Task<ObservableCollection<FileSystemItem>> LoadProjectRecursivelyAsync(string rootPath)
	{
		return await Task.Run(() =>
		{
			var items = new ObservableCollection<FileSystemItem>();
			if (!Directory.Exists(rootPath)) return items;

			var rootDir = new DirectoryInfo(rootPath);

			// Carrega o conteúdo da raiz recursivamente
			var children = LoadDirectoryInternal(rootDir);

			foreach (var child in children)
			{
				items.Add(child);
			}

			return items;
		});
	}

	private List<FileSystemItem> LoadDirectoryInternal(DirectoryInfo dir)
	{
		var items = new List<FileSystemItem>();

		try
		{
			// 1. Diretórios (Recursão)
			var subDirs = dir.GetDirectories()
							 .Where(d => !IgnoredFolders.Contains(d.Name))
							 .OrderBy(d => d.Name);

			foreach (var subDir in subDirs)
			{
				var folderItem = CreateFileSystemItem(subDir);

				// AQUI ESTÁ A MÁGICA: Já carregamos os filhos imediatamente
				var children = LoadDirectoryInternal(subDir);
				foreach (var child in children)
				{
					folderItem.Children.Add(child);
				}

				items.Add(folderItem);
			}

			// 2. Arquivos
			var files = dir.GetFiles().OrderBy(f => f.Name);
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
	}

	private FileSystemItem CreateFileSystemItem(FileSystemInfo info)
	{
		var item = new FileSystemItem
		{
			Name = info.Name,
			FullPath = info.FullName,
			IsDirectory = info is DirectoryInfo,
			IsExpanded = false,
			Children = new ObservableCollection<FileSystemItem>() // Inicializa vazio
		};

		if (!item.IsDirectory && info is FileInfo fileInfo)
		{
			item.FileSize = fileInfo.Length;
		}

		return item;
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