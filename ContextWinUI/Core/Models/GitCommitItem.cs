using System;
using System.Collections.Generic;

namespace ContextWinUI.Core.Models;

public class GitCommitItem
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha => Sha.Length > 7 ? Sha.Substring(0, 7) : Sha;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string FormattedDate => Date.ToString("g");
    public List<string> ParentShas { get; set; } = new();
}
