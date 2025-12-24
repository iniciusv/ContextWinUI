namespace ContextWinUI.Services;

public enum DiffType
{
	Unchanged,
	Added,
	Deleted,
	Placeholder // Usado para alinhamento se fizermos side-by-side, mas aqui faremos unified
}
