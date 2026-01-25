using ContextWinUI.Core.Contracts;
using ContextWinUI.Core.Models;
using ContextWinUI.Features.GraphParser.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Services;

public interface IProjectSearchService
{
    Task<List<SearchSuggestion>> SearchAsync(string query, string rootPath, ISemanticIndexService indexService);
}

public class ProjectSearchService : IProjectSearchService
{
    public async Task<List<SearchSuggestion>> SearchAsync(string query, string rootPath, ISemanticIndexService indexService)
    {
        return await Task.Run(() =>
        {
            var suggestions = new List<SearchSuggestion>();
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrEmpty(rootPath))
            {
                return suggestions;
            }

            try
            {
                var graph = indexService.GetCurrentGraph();
                if (graph == null || !graph.Nodes.Any())
                {
                    return SearchInDirectory(query, rootPath);
                }

                var queryLower = query.ToLowerInvariant();
                
                var nodesByFile = graph.Nodes.Values
                    .GroupBy(node => graph.GetFilePath(node.FileId))
                    .Where(group => !string.IsNullOrEmpty(group.Key) && File.Exists(group.Key));

                foreach (var fileGroup in nodesByFile)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(fileGroup.Key)) continue;
                        var fileName = Path.GetFileName(fileGroup.Key);
                        string filePath = fileGroup.Key;

                        var matchingNodes = fileGroup
                            .Where(node =>
                                node.Name.ToLowerInvariant().Contains(queryLower) ||
                                node.Type.ToString().ToLowerInvariant().Contains(queryLower) ||
                                fileName.ToLowerInvariant().Contains(queryLower))
                            .ToList();

                        if (matchingNodes.Any())
                        {
                            string relativePath = Path.GetRelativePath(rootPath, filePath);
                            var symbolsFound = string.Join(", ", matchingNodes.Select(n => $"{n.Type}: {n.Name}").Take(3));

                            suggestions.Add(new SearchSuggestion
                            {
                                Title = fileName,
                                Subtitle = $"Found: {symbolsFound}",
                                FilePath = relativePath,
                                Icon = "\uE943",
                                MatchCount = matchingNodes.Count
                            });
                        }
                    }
                    catch { }
                }

                suggestions = suggestions.OrderByDescending(s => s.MatchCount).ThenBy(s => s.Title).Take(15).ToList();

                if (!suggestions.Any())
                {
                    return SearchInDirectory(query, rootPath);
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SearchAsync: {ex.Message}");
                return SearchInDirectory(query, rootPath);
            }
        });
    }

    private List<SearchSuggestion> SearchInDirectory(string query, string rootPath)
    {
        var suggestions = new List<SearchSuggestion>();
        try
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return suggestions;

            var files = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .Where(f => Path.GetFileName(f).Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(15)
                .Select(f => new SearchSuggestion
                {
                    Title = Path.GetFileName(f),
                    Subtitle = "File in directory",
                    FilePath = Path.GetRelativePath(rootPath, f),
                    Icon = "\uE943",
                    MatchCount = 1
                });

            suggestions.AddRange(files);
        }
        catch { }
        return suggestions;
    }
}
