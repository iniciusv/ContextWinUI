using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.GraphParser;

public interface IVersionDiffManager
{
	void RefreshReferenceColumns(IEnumerable<SegmentRowViewModel> rows, IList<GlobalVersion> globalHistory, int globalVersionIndex);
	void UpdateRowsVisibility(IEnumerable<SegmentRowViewModel> rows, bool hideUnchanged);
	void RestoreBlocksToGlobalVersion(IEnumerable<SegmentRowViewModel> rows, GlobalVersion targetVersion);
}
