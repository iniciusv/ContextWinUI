using ContextWinUI.Core.Models;
using System;
using System.Collections.Generic;

namespace ContextWinUI.Features.GraphView;

public interface ITokenDiffEngine
{
	List<TokenChange> ComputeDiff(List<SymbolNode> source, List<SymbolNode> target, Func<SymbolNode, SymbolNode, bool> similarityPredicate);
}
