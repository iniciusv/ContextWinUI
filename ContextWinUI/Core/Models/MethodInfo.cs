using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace ContextWinUI.Models;

public partial class MethodInfo : ObservableObject
{
	[ObservableProperty]
	private string name = string.Empty;

	[ObservableProperty]
	private string fullSignature = string.Empty;

	[ObservableProperty]
	private string filePath = string.Empty;

	[ObservableProperty]
	private string sourceCode = string.Empty;

	[ObservableProperty]
	private bool isSelected;

	public List<string> CalledMethods { get; set; } = new();
	public List<string> UsedTypes { get; set; } = new();
	public List<string> UsedNamespaces { get; set; } = new();
	public List<string> AccessedMembers { get; set; } = new();

	public string DependenciesSummary
	{
		get
		{
			var count = CalledMethods.Count + UsedTypes.Count + AccessedMembers.Count;
			return $"{count} dependência(s)";
		}
	}
}