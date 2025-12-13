using ContextWinUI.ContextWinUI.Models;
using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public class PersistenceService : IPersistenceService
{
	// Pasta onde os caches serão salvos (dentro da pasta do App)
	private readonly string _cacheDirectory;

	public PersistenceService()
	{
		// Define pasta "Cache" ao lado do executável
		var appPath = AppDomain.CurrentDomain.BaseDirectory;
		_cacheDirectory = Path.Combine(appPath, "UserCache");

		if (!Directory.Exists(_cacheDirectory))
			Directory.CreateDirectory(_cacheDirectory);
	}

	public async Task SaveProjectCacheAsync(string projectRootPath, IEnumerable<FileSharedState> states)
	{
		// Filtra apenas arquivos que têm algo relevante para salvar (ex: Tags ou marcados)
		// Se quiser salvar TUDO para acelerar o load da árvore, remova o Where.
		var relevantItems = states.Where(s => s.Tags.Any() || s.IsChecked).ToList();

		if (!relevantItems.Any()) return;

		var dto = new ProjectCacheDto
		{
			RootPath = projectRootPath,
			Files = relevantItems.Select(s => new FileMetadataDto
			{
				// Salvamos caminho relativo para o cache funcionar mesmo se movermos a pasta do projeto
				RelativePath = Path.GetRelativePath(projectRootPath, s.FullPath),
				Tags = s.Tags.ToList()
			}).ToList()
		};

		var filePath = GetCacheFilePath(projectRootPath);
		var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });

		await File.WriteAllTextAsync(filePath, json);
	}

	public async Task<ProjectCacheDto?> LoadProjectCacheAsync(string projectRootPath)
	{
		var filePath = GetCacheFilePath(projectRootPath);

		if (!File.Exists(filePath)) return null;

		try
		{
			var json = await File.ReadAllTextAsync(filePath);
			return JsonSerializer.Deserialize<ProjectCacheDto>(json);
		}
		catch
		{
			return null; // Cache corrompido ou inválido
		}
	}

	// Gera um nome de arquivo único baseado no caminho do projeto
	private string GetCacheFilePath(string projectRootPath)
	{
		using var md5 = MD5.Create();
		var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(projectRootPath.ToLowerInvariant()));
		var hex = BitConverter.ToString(hash).Replace("-", "").ToLower();
		return Path.Combine(_cacheDirectory, $"proj_{hex}.json");
	}
}