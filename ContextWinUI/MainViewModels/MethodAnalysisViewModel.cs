using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ContextWinUI.Models;
using ContextWinUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ContextWinUI.ViewModels;

public partial class MethodAnalysisViewModel : ObservableObject
{
	private readonly RoslynAnalyzerService _roslynAnalyzer;

	[ObservableProperty]
	private ObservableCollection<MethodInfo> methods = new();

	[ObservableProperty]
	private bool isVisible;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private MethodInfo? selectedMethod;

	[ObservableProperty]
	private string methodDependenciesText = string.Empty;

	public event EventHandler<string>? StatusChanged;

	public MethodAnalysisViewModel(RoslynAnalyzerService roslynAnalyzer)
	{
		_roslynAnalyzer = roslynAnalyzer;
	}


	[RelayCommand]
	private void SelectMethod(MethodInfo? method)
	{
		if (method == null)
			return;

		SelectedMethod = method;
		MethodDependenciesText = BuildDependenciesText(method);
	}

	private string BuildDependenciesText(MethodInfo method)
	{
		var sb = new StringBuilder();
		sb.AppendLine($"Método: {method.FullSignature}");
		sb.AppendLine($"Arquivo: {method.FilePath}");
		sb.AppendLine();

		if (method.UsedNamespaces.Any())
		{
			sb.AppendLine("📦 Namespaces Utilizados:");
			foreach (var ns in method.UsedNamespaces.OrderBy(n => n))
			{
				sb.AppendLine($"  • {ns}");
			}
			sb.AppendLine();
		}

		if (method.CalledMethods.Any())
		{
			sb.AppendLine("🔗 Métodos Chamados:");
			foreach (var m in method.CalledMethods.OrderBy(m => m))
			{
				sb.AppendLine($"  • {m}");
			}
			sb.AppendLine();
		}

		if (method.UsedTypes.Any())
		{
			sb.AppendLine("📋 Tipos Utilizados:");
			foreach (var t in method.UsedTypes.OrderBy(t => t))
			{
				sb.AppendLine($"  • {t}");
			}
			sb.AppendLine();
		}

		if (method.AccessedMembers.Any())
		{
			sb.AppendLine("🔑 Membros Acessados:");
			foreach (var member in method.AccessedMembers.OrderBy(m => m))
			{
				sb.AppendLine($"  • {member}");
			}
			sb.AppendLine();
		}

		sb.AppendLine("📝 Código Fonte:");
		sb.AppendLine(method.SourceCode);

		return sb.ToString();
	}

	[RelayCommand]
	private async Task CopyMethodWithDependenciesAsync(MethodInfo? method)
	{
		if (method == null)
			return;

		IsLoading = true;
		OnStatusChanged("Coletando dependências...");

		try
		{
			var sb = new StringBuilder();

			sb.AppendLine($"// Método: {method.Name}");
			sb.AppendLine($"// Arquivo: {method.FilePath}");
			sb.AppendLine();

			if (method.UsedNamespaces.Any())
			{
				sb.AppendLine("// Namespaces necessários:");
				foreach (var ns in method.UsedNamespaces.OrderBy(n => n))
				{
					sb.AppendLine($"using {ns};");
				}
				sb.AppendLine();
			}

			sb.AppendLine(method.SourceCode);

			var dataPackage = new DataPackage();
			dataPackage.SetText(sb.ToString());
			Clipboard.SetContent(dataPackage);

			OnStatusChanged("Método e dependências copiados!");
		}
		catch (Exception ex)
		{
			OnStatusChanged($"Erro ao copiar: {ex.Message}");
		}
		finally
		{
			IsLoading = false;
		}
	}

	[RelayCommand]
	private void Close()
	{
		IsVisible = false;
		Methods.Clear();
		SelectedMethod = null;
		MethodDependenciesText = string.Empty;
	}

	partial void OnSelectedMethodChanged(MethodInfo? value)
	{
		if (value != null)
		{
			SelectMethod(value);
		}
	}

	private void OnStatusChanged(string message)
	{
		StatusChanged?.Invoke(this, message);
	}
}