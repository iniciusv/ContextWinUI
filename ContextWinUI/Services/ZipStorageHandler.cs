using ContextWinUI.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace ContextWinUI.Services
{
    public class ZipStorageHandler : IStorageFormatHandler
    {
        private const string MainEntryName = "project_cache.json";

        public async Task SaveAsync(string filePath, ProjectCacheDto data)
        {
            // Always save as ZIP (container)
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

            var entry = archive.CreateEntry(MainEntryName);
            using var entryStream = entry.Open();
            
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            await JsonSerializer.SerializeAsync(entryStream, data, jsonOptions);
        }

        public async Task<ProjectCacheDto?> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            // Detect format
            if (IsZipFile(filePath))
            {
                return await LoadFromZipAsync(filePath);
            }
            else
            {
                return await LoadFromJsonAsync(filePath);
            }
        }

        private bool IsZipFile(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[4];
                if (stream.Read(buffer, 0, 4) < 4) return false;
                
                // PK signature: 50 4B 03 04
                return buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04;
            }
            catch
            {
                return false;
            }
        }

        private async Task<ProjectCacheDto?> LoadFromZipAsync(string filePath)
        {
            try
            {
                using var fileStream = File.OpenRead(filePath);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
                
                var entry = archive.GetEntry(MainEntryName);
                if (entry == null) return null;

                using var entryStream = entry.Open();
                return await JsonSerializer.DeserializeAsync<ProjectCacheDto>(entryStream);
            }
            catch
            {
                return null;
            }
        }

        private async Task<ProjectCacheDto?> LoadFromJsonAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<ProjectCacheDto>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
