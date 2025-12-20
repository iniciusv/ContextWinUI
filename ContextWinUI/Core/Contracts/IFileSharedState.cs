using System.Collections.Generic;

namespace ContextWinUI.Core.Contracts;

public interface IFileSharedState
{
	string FullPath { get; }
	string Name { get; }
	bool IsChecked { get; }
	bool IsIgnored { get; }
	long? FileSize { get; }
	IEnumerable<string> Tags { get; }
}