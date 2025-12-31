using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Features.GraphParser // Ajuste o namespace conforme sua estrutura
{
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

		private T DeepCopy(T item)
		{
			try
			{
				var json = System.Text.Json.JsonSerializer.Serialize(item);
				return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
			}
			catch
			{
				return item;
			}
		}
	}
}