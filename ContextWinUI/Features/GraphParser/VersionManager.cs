using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public class VersionManager<T> where T : class
{
	private readonly Dictionary<string, List<CodeBlockVersion>> _versions = new();
	private readonly Dictionary<string, T> _originalItems = new();

	public void RegisterOriginal(string itemId, T item, string initialContent)
	{
		if (!_versions.ContainsKey(itemId))
		{
			_versions[itemId] = new List<CodeBlockVersion>();
			_originalItems[itemId] = DeepCopy(item);

			// Adiciona versão original
			_versions[itemId].Add(new CodeBlockVersion
			{
				Content = initialContent,
				Description = "Versão Original",
				Timestamp = DateTime.Now,
				IsOriginal = true
			});
		}
	}

	public void CreateNewVersion(string itemId, string content, string description = "Modificado")
	{
		if (_versions.ContainsKey(itemId))
		{
			var newVersion = _versions[itemId].Last().Clone();
			newVersion.Content = content;
			newVersion.Description = description;
			newVersion.Timestamp = DateTime.Now;

			_versions[itemId].Add(newVersion);
		}
	}

	public List<CodeBlockVersion> GetVersions(string itemId)
	{
		return _versions.ContainsKey(itemId)
			? _versions[itemId]
			: new List<CodeBlockVersion>();
	}

	public CodeBlockVersion? GetVersion(string itemId, int versionIndex)
	{
		if (_versions.ContainsKey(itemId) && versionIndex >= 0 && versionIndex < _versions[itemId].Count)
		{
			return _versions[itemId][versionIndex];
		}
		return null;
	}

	public CodeBlockVersion? GetOriginalVersion(string itemId)
	{
		return _versions.ContainsKey(itemId)
			? _versions[itemId].FirstOrDefault(v => v.IsOriginal)
			: null;
	}

	public T? GetOriginalItem(string itemId)
	{
		return _originalItems.ContainsKey(itemId)
			? DeepCopy(_originalItems[itemId])
			: null;
	}

	public bool HasVersions(string itemId)
	{
		return _versions.ContainsKey(itemId) && _versions[itemId].Count > 1;
	}

	public void ClearVersions(string itemId)
	{
		if (_versions.ContainsKey(itemId))
		{
			// Mantém apenas a versão original
			var original = _versions[itemId].FirstOrDefault(v => v.IsOriginal);
			_versions[itemId] = original != null
				? new List<CodeBlockVersion> { original }
				: new List<CodeBlockVersion>();
		}
	}

	private T DeepCopy(T item)
	{
		// Implementação de deep copy (pode usar JSON serialização como fallback)
		try
		{
			var json = System.Text.Json.JsonSerializer.Serialize(item);
			return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
		}
		catch
		{
			// Fallback: retorna o mesmo objeto (em produção, usar um método mais robusto)
			return item;
		}
	}
}
