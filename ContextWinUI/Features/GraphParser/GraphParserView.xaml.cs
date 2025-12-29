// Code Behind: GraphParserView.xaml.cs
using ContextWinUI.Features.GraphParser.Models;
using ContextWinUI.Features.GraphParser.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace ContextWinUI.Features.GraphParser.Views;

public sealed partial class GraphParserView : UserControl
{
	public GraphParserViewModel ViewModel
	{
		get => (GraphParserViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	public static readonly DependencyProperty ViewModelProperty =
		DependencyProperty.Register(nameof(ViewModel), typeof(GraphParserViewModel), typeof(GraphParserView), new PropertyMetadata(null));

	public GraphParserView()
	{
		this.InitializeComponent();
		this.Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (this.DataContext == null && ViewModel != null)
		{
			this.DataContext = ViewModel;
		}
	}

	private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
{
		Debug.WriteLine($"TextChanged: {sender.Text}, Reason: {args.Reason}");

		if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
		{
			if (ViewModel != null && ViewModel.UpdateSearchCommand.CanExecute(sender.Text))
			{
				ViewModel.UpdateSearchCommand.Execute(sender.Text);
			}
			else
			{
				Debug.WriteLine("ViewModel ou Command é null");
			}
		}
	}

	// ARQUIVO: GraphParserView.xaml.cs (atualizado)
	private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
	{
		if (args.SelectedItem is SearchSuggestion suggestion)
		{
			ViewModel.OpenFileCommand.Execute(suggestion.FilePath);
			sender.Text = string.Empty;
		}
		else if (args.SelectedItem is string filePath)
		{
			// Mantenha compatibilidade com versões anteriores
			ViewModel.OpenFileCommand.Execute(filePath);
			sender.Text = string.Empty;
		}
	}

	private void TabView_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
	{
		if (args.Item is FileSegmentsViewModel tabVm)
		{
			ViewModel.CloseTabCommand.Execute(tabVm);
		}
	}
}