using ContextWinUI.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace ContextWinUI.Services;

/// <summary>
/// FACTORY / CACHE: Gerencia o ciclo de vida dos Flyweights.
/// </summary>
public class FileSystemItemFactory: IFileSystemItemFactory
{
	// Dicionário thread-safe para garantir unicidade baseada no caminho
	private readonly ConcurrentDictionary<string, FileSharedState> _sharedStates = new();

	/// <summary>
	/// Cria ou recupera um estado compartilhado existente e retorna um novo Wrapper (FileSystemItem).
	/// </summary>
	public FileSystemItem CreateWrapper(string fullPath, FileSystemItemType type, string? customIcon = null)
	{
		// Normaliza o caminho para evitar duplicatas por casing (c:\abc vs C:\ABC)
		string key = fullPath.ToLowerInvariant();

		// Obtém ou cria o Flyweight
		var sharedState = _sharedStates.GetOrAdd(key, k => new FileSharedState(fullPath));

		// Retorna o Wrapper (Nó da árvore) que aponta para esse estado
		return new FileSystemItem(sharedState)
		{
			Type = type,
			CustomIcon = customIcon
		};
	}

	/// <summary>
	/// Cria um wrapper a partir de um FileInfo (usado no Explorer)
	/// </summary>
	public FileSystemItem CreateWrapper(FileSystemInfo info)
	{
		var type = info is DirectoryInfo ? FileSystemItemType.Directory : FileSystemItemType.File;
		var item = CreateWrapper(info.FullName, type);

		if (info is FileInfo fi)
		{
			// Atualiza o tamanho no estado compartilhado (se ainda não tiver)
			if (item.SharedState.FileSize == null)
				item.SharedState.FileSize = fi.Length;
		}

		return item;
	}

	public IEnumerable<FileSharedState> GetAllStates()
	{
		return _sharedStates.Values;
	}

	public void ClearCache()
	{
		_sharedStates.Clear();
	}
}