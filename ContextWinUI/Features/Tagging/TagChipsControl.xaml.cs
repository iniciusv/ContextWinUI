using ContextWinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace ContextWinUI.Views.Components;

public sealed partial class TagChipsControl : UserControl
{
	// Coleção interna de Wrappers (Nome + Cor) usada pelo XAML
	public ObservableCollection<TagUiWrapper> DisplayTags { get; } = new();

	// Propriedade de Dependência para receber a lista de strings do ViewModel
	public static readonly DependencyProperty TagsSourceProperty =
		DependencyProperty.Register(nameof(TagsSource), typeof(ObservableCollection<string>), typeof(TagChipsControl), new PropertyMetadata(null, OnTagsSourceChanged));

	public ObservableCollection<string> TagsSource
	{
		get => (ObservableCollection<string>)GetValue(TagsSourceProperty);
		set => SetValue(TagsSourceProperty, value);
	}

	public TagChipsControl()
	{
		this.InitializeComponent();
	}

	private static void OnTagsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		var control = (TagChipsControl)d;
		control.SyncTags();

		if (e.NewValue is ObservableCollection<string> newCollection)
		{
			newCollection.CollectionChanged += control.OnSourceCollectionChanged;
		}
		if (e.OldValue is ObservableCollection<string> oldCollection)
		{
			oldCollection.CollectionChanged -= control.OnSourceCollectionChanged;
		}
	}

	private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		SyncTags();
	}

	// Sincroniza a lista de strings (TagsSource) com a lista visual (DisplayTags)
	private void SyncTags()
	{
		DisplayTags.Clear();
		if (TagsSource != null)
		{
			foreach (var tag in TagsSource)
			{
				DisplayTags.Add(new TagUiWrapper(tag));
			}
		}
	}

	// Evento disparado pelo ColorPicker no XAML
	private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
	{
		// O DataContext do ColorPicker é o próprio Wrapper da tag clicada
		if (sender.DataContext is TagUiWrapper wrapper)
		{
			TagColorService.Instance.SetColorForTag(wrapper.Name, args.NewColor);
		}
	}
}