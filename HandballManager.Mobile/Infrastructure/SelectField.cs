using System.Collections;
using Microsoft.Maui.Controls.Shapes;

namespace HandballManager.Mobile;

/// <summary>
/// App-themed replacement for <see cref="Picker"/>. Renders as a dark rounded field with a
/// chevron; tapping opens a styled modal list (<see cref="SelectSheetPage"/>) instead of the
/// platform's native picker dialog.
/// </summary>
public class SelectField : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SelectField), string.Empty, propertyChanged: OnChanged);
    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SelectField));
    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SelectField), null, BindingMode.TwoWay, propertyChanged: OnChanged);
    public static readonly BindableProperty DisplayPathProperty =
        BindableProperty.Create(nameof(DisplayPath), typeof(string), typeof(SelectField), null);

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string? DisplayPath { get => (string?)GetValue(DisplayPathProperty); set => SetValue(DisplayPathProperty, value); }

    private readonly Label _value;

    public SelectField()
    {
        _value = new Label { FontSize = 15, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
        var chevron = new Image { Source = "dropdown.png", WidthRequest = 12, HeightRequest = 12, Opacity = 0.7, VerticalOptions = LayoutOptions.Center };

        var grid = new Grid
        {
            Padding = new Thickness(14, 0),
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
        };
        grid.Add(_value, 0, 0);
        grid.Add(chevron, 1, 0);

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#16213E"),
            Stroke = Color.FromArgb("#3A5578"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            HeightRequest = 48,
            Content = grid,
        };
        border.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await OpenAsync()) });

        Content = border;
        UpdateDisplay();
    }

    private string TextFor(object? item)
    {
        if (item == null) return string.Empty;
        if (!string.IsNullOrEmpty(DisplayPath))
            return item.GetType().GetProperty(DisplayPath)?.GetValue(item)?.ToString() ?? item.ToString() ?? "";
        return item.ToString() ?? "";
    }

    private void UpdateDisplay()
    {
        if (SelectedItem != null)
        {
            _value.Text = TextFor(SelectedItem);
            _value.TextColor = Color.FromArgb("#EAEAEA");
        }
        else
        {
            _value.Text = string.IsNullOrEmpty(Title) ? "Select…" : Title;
            _value.TextColor = Color.FromArgb("#8888AA");
        }
    }

    private static void OnChanged(BindableObject b, object o, object n) => ((SelectField)b).UpdateDisplay();

    private async Task OpenAsync()
    {
        if (ItemsSource == null) return;
        var items = ItemsSource.Cast<object>().ToList();
        if (items.Count == 0) return;

        var host = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (host == null) return;

        var (chosen, value) = await SelectSheetPage.ShowAsync(host, string.IsNullOrEmpty(Title) ? "Select" : Title, items, SelectedItem, TextFor);
        if (chosen && !Equals(value, SelectedItem))
            SelectedItem = value;
    }
}

/// <summary>Modal list styled to the app palette, used by <see cref="SelectField"/>.</summary>
public class SelectSheetPage : ContentPage
{
    private readonly TaskCompletionSource<(bool Chosen, object? Value)> _tcs = new();
    private bool _closing;

    public static async Task<(bool Chosen, object? Value)> ShowAsync(
        Page host, string title, IList<object> items, object? selected, Func<object?, string> textFor)
    {
        var page = new SelectSheetPage(title, items, selected, textFor);
        await host.Navigation.PushModalAsync(page, false);
        return await page._tcs.Task;
    }

    private SelectSheetPage(string title, IList<object> items, object? selected, Func<object?, string> textFor)
    {
        BackgroundColor = Color.FromArgb("#EE121826");

        var rows = items.Select(it => new SheetRow(it, textFor(it), Equals(it, selected))).ToList();

        var list = new CollectionView
        {
            ItemsSource = rows,
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontSize = 15, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
                name.SetBinding(Label.TextProperty, nameof(SheetRow.Text));
                name.SetBinding(Label.TextColorProperty, nameof(SheetRow.TextColor));

                var dot = new Label { Text = "●", FontSize = 12, TextColor = Color.FromArgb("#E94560"), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
                dot.SetBinding(IsVisibleProperty, nameof(SheetRow.IsSelected));

                var g = new Grid { Padding = new Thickness(14, 12), ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
                g.Add(name, 0, 0);
                g.Add(dot, 1, 0);

                var b = new Border
                {
                    BackgroundColor = Color.FromArgb("#16213E"),
                    Stroke = Color.FromArgb("#22304F"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Margin = new Thickness(0, 3),
                    Content = g,
                };
                var tap = new TapGestureRecognizer { Command = new Command<SheetRow>(r => Close(true, r?.Value)) };
                tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");
                b.GestureRecognizers.Add(tap);
                return b;
            }),
        };

        var titleLabel = new Label
        {
            Text = title.ToUpperInvariant(),
            TextColor = Color.FromArgb("#E94560"),
            FontFamily = "OpenSansSemibold",
            FontSize = 13,
            CharacterSpacing = 2,
            Margin = new Thickness(4, 0, 0, 10),
        };

        var cancel = new Button
        {
            Text = "CANCEL",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#8888AA"),
            BorderColor = Color.FromArgb("#3A5578"),
            BorderWidth = 1,
            FontFamily = "OpenSansSemibold",
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 10, 0, 0),
        };
        cancel.Clicked += (_, __) => Close(false, null);

        var cardGrid = new Grid
        {
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            RowSpacing = 0,
        };
        cardGrid.Add(titleLabel, 0, 0);
        cardGrid.Add(list, 0, 1);
        cardGrid.Add(cancel, 0, 2);

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#1A1A2E"),
            Stroke = Color.FromArgb("#3A5578"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16, 16),
            Margin = new Thickness(20),
            MaximumHeightRequest = 520,
            VerticalOptions = LayoutOptions.Center,
            Content = cardGrid,
        };

        var root = new Grid();
        // tap outside the card dismisses
        root.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Close(false, null)) });
        root.Add(card);

        Content = root;
    }

    private async void Close(bool chosen, object? value)
    {
        if (_closing) return;
        _closing = true;
        await Navigation.PopModalAsync(false);
        _tcs.TrySetResult((chosen, value));
    }

    protected override bool OnBackButtonPressed()
    {
        Close(false, null);
        return true;
    }

    private sealed class SheetRow
    {
        public object? Value { get; }
        public string Text { get; }
        public bool IsSelected { get; }
        public Color TextColor => IsSelected ? Color.FromArgb("#E94560") : Color.FromArgb("#EAEAEA");

        public SheetRow(object? value, string text, bool isSelected)
        {
            Value = value;
            Text = text;
            IsSelected = isSelected;
        }
    }
}
