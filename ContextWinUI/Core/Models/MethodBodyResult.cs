using System.Collections.Generic;

namespace ContextWinUI.Core.Models;

public class MethodBodyResult
{
	public List<string> InternalMethodCalls { get; set; } = new();
	public List<string> AccessedProperties { get; set; } = new();
	public Dictionary<string, string> ExternalDependencies { get; set; } = new();
}