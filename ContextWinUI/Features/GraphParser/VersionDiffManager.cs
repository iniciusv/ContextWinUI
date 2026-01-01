using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContextWinUI.Features.GraphParser.Services;

public class VersionDiffManager: IVersionDiffManager
{

	public void RefreshReferenceColumns(
		IEnumerable<SegmentRowViewModel> rows,
		IList<GlobalVersion> globalHistory,
		int globalVersionIndex)
	{
		var targetGlobalVersion = globalHistory.ElementAtOrDefault(globalVersionIndex);
		if (targetGlobalVersion == null) return;

		DateTime cutoffTime = targetGlobalVersion.IsOriginal ? DateTime.MinValue : targetGlobalVersion.Timestamp;
		// Pequena tolerância para batches
		if (cutoffTime > DateTime.MinValue) cutoffTime = cutoffTime.AddMilliseconds(100);

		foreach (var row in rows)
		{
			var currentBlock = row.Current;
			// Busca a última versão que existia naquele momento no tempo
			var pastVersion = currentBlock.Versions.LastOrDefault(v =>
				v.IsOriginal || v.Timestamp <= cutoffTime);

			if (pastVersion != null)
			{
				var refBlock = currentBlock.Clone();
				refBlock.Content = pastVersion.Content;
				refBlock.Timestamp = pastVersion.Timestamp;
				refBlock.InitializeVersions(pastVersion.Content);
				refBlock.TypeDescription = "REF";
				if (pastVersion.IsOriginal) refBlock.Name += " (Orig)";

				row.Reference = refBlock;
			}
			else
			{
				row.Reference = null;
			}
		}
	}

	public void UpdateRowsVisibility(IEnumerable<SegmentRowViewModel> rows, bool hideUnchanged)
	{
		foreach (var row in rows)
		{
			if (!hideUnchanged)
			{
				row.IsVisible = true;
				continue;
			}

			var block = row.Current;
			// Considera modificado se tem changes não salvas OU se tem histórico de versões
			bool isModified = block.HasUnsavedChanges || block.Versions.Count > 1;

			// Se ainda parece não modificado, checa contra a referência visual (diff mode)
			if (!isModified && row.Reference != null)
			{
				isModified = block.Content != row.Reference.Content;
			}

			row.IsVisible = isModified;
		}
	}

	public void RestoreBlocksToGlobalVersion(IEnumerable<SegmentRowViewModel> rows, GlobalVersion targetVersion)
	{
		var cutoffTime = targetVersion.IsOriginal ? DateTime.MinValue : targetVersion.Timestamp;
		var safeCutoff = cutoffTime == DateTime.MinValue ? DateTime.MinValue : cutoffTime.AddMilliseconds(100);

		foreach (var row in rows)
		{
			var block = row.Current;
			var bestVersion = block.Versions.LastOrDefault(v => v.IsOriginal || v.Timestamp <= safeCutoff);

			if (bestVersion != null)
			{
				int index = block.Versions.IndexOf(bestVersion);
				block.RestoreVersion(index);
			}
			else
			{
				block.RestoreVersion(0);
			}
		}
	}
}