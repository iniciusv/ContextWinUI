using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser.Interfaces;

/// <summary>
/// Defines a service responsible for initializing and managing Git comparison sessions for file segments.
/// </summary>
public interface IGitComparisonService
{
    /// <summary>
    /// Initializes a comparison session by parsing current and old content, setting up global history, 
    /// and aligning blocks between versions.
    /// </summary>
    /// <param name="filePath">Target file path.</param>
    /// <param name="contentCurrent">The current (newer) content.</param>
    /// <param name="contentOld">The old (original) content to compare against.</param>
    /// <param name="oldVersionLabel">Label for the old version (e.g. Commit SHA).</param>
    /// <returns>A tuple containing the initialized row view models and the generated global history.</returns>
    Task<(ObservableCollection<SegmentRowViewModel> Rows, ObservableCollection<GlobalVersion> History)> InitializeComparisonAsync(
        string filePath, 
        string contentCurrent, 
        string contentOld, 
        string oldVersionLabel);
}
