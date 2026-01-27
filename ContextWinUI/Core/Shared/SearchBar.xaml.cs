// ARQUIVO: SearchBar.xaml.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections; // Necessário para IEnumerable não genérico
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ContextWinUI.Views.Components;

public sealed partial class SearchBar : UserControl
{
	public static readonly DependencyProperty AvailableTagsProperty =
			DependencyProperty.Register(
				nameof(AvailableTags),
				typeof(object),
				typeof(SearchBar),
				new PropertyMetadata(null, OnAvailableTagsChanged)); // <--- Callback aqui

	public object AvailableTags
	{
		get => GetValue(AvailableTagsProperty);
		set => SetValue(AvailableTagsProperty, value);
	}

	private static void OnAvailableTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SearchBar searchBar)
		{
			var count = 0;
			if (e.NewValue is ICollection collection) count = collection.Count;
			else if (e.NewValue is IEnumerable enumerable) count = enumerable.Cast<object>().Count();

			// Coloque um breakpoint AQUI para ver quando os dados chegam
			System.Diagnostics.Debug.WriteLine($"[SearchBar Binding] Dados recebidos. Contagem: {count}");
		}
	}

	public static readonly DependencyProperty SearchCommandProperty = DependencyProperty.Register(nameof(SearchCommand), typeof(ICommand), typeof(SearchBar), new PropertyMetadata(null));
	public ICommand SearchCommand
	{
		get => (ICommand)GetValue(SearchCommandProperty);
		set => SetValue(SearchCommandProperty, value);
	}

	public static readonly DependencyProperty SubmitCommandProperty = DependencyProperty.Register(nameof(SubmitCommand), typeof(ICommand), typeof(SearchBar), new PropertyMetadata(null));
	public ICommand SubmitCommand
	{
		get => (ICommand)GetValue(SubmitCommandProperty);
		set => SetValue(SubmitCommandProperty, value);
	}

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(SearchBar), new PropertyMetadata("Search..."));
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

	public SearchBar()
	{
		this.InitializeComponent();
	}

	private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
	{
		if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			// Executa o comando de busca em tempo real (para filtrar a árvore)
			if (SearchCommand != null && SearchCommand.CanExecute(sender.Text))
			{
				SearchCommand.Execute(sender.Text);
			}

			// Atualiza as sugestões do AutoSuggestBox
			UpdateSuggestions(sender);
		}
	}

	private void UpdateSuggestions(AutoSuggestBox sender)
	{
		// Pega o valor atual da propriedade (que foi atualizado pelo Binding)
		var source = AvailableTags as IEnumerable;

		if (source == null) return;

		// Converte para lista de strings para facilitar manipulação
		var tags = source.Cast<object>().Select(x => x.ToString()).ToList();

		if (!tags.Any()) return;

		string text = sender.Text;
		string prefix = "";

		if (text.StartsWith("[]#")) prefix = "[]#";
		else if (text.StartsWith("+#")) prefix = "+#";
		else if (text.StartsWith("-#")) prefix = "-#";
		else if (text.StartsWith("#")) prefix = "#";

		if (string.IsNullOrEmpty(prefix))
		{
			sender.ItemsSource = null;
			return;
		}

		string typed = text.Substring(prefix.Length);

		// Filtro
		var matches = tags
			.Where(t => t != null && t.Contains(typed, System.StringComparison.OrdinalIgnoreCase))
			.Select(t => $"{prefix}{t}")
			.ToList();

		sender.ItemsSource = matches.Any() ? matches : null;
	}

	private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		if (args.SelectedItem is string selectedText && selectedText != "Nenhuma tag encontrada...")
		{
			sender.Text = selectedText;
			// Opcional: Disparar a busca imediatamente ao escolher
			if (SearchCommand != null && SearchCommand.CanExecute(selectedText))
			{
				SearchCommand.Execute(selectedText);
			}
		}
	}

	private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
	{
		if (SubmitCommand != null && SubmitCommand.CanExecute(sender.Text))
		{
			SubmitCommand.Execute(sender.Text);
		}
	}
}