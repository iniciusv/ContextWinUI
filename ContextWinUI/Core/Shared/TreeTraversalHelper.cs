using ContextWinUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContextWinUI.Core.Shared;
public static class TreeTraversalHelper
{
	public static IEnumerable<FileSystemItem> GetCheckedItems(IEnumerable<FileSystemItem> items)
	{
		foreach (var item in items)
		{
			if (item.IsChecked && item.IsCodeFile) yield return item;
			foreach (var child in GetCheckedItems(item.Children)) yield return child;
		}
	}

	public static int CountCheckedFiles(IEnumerable<FileSystemItem> items)
	{
		int count = 0;
		foreach (var item in items)
		{
			if (item.IsChecked && item.IsCodeFile) count++;
			count += CountCheckedFiles(item.Children);
		}
		return count;
	}

	public static void SetAllChecked(IEnumerable<FileSystemItem> items, bool isChecked)
	{
		foreach (var item in items)
		{
			if (item.IsCodeFile) item.IsChecked = isChecked;
			SetAllChecked(item.Children, isChecked);
		}
	}
}
