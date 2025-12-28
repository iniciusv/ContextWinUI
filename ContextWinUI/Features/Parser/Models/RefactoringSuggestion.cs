using ContextWinUI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Features.Parser.Models;

public class RefactoringSuggestion
{
	public SymbolNode TargetSymbol { get; set; }
	public string NewCode { get; set; }

	public double Confidence { get; set; }

	public string MatchTypeDescription { get; set; }

	public string OriginalCode { get; set; }
	public bool IsSafeToApply { get; set; }
}