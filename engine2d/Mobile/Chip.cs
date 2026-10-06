namespace HandballManager.Mobile.Match2D;

/// <summary>
/// A small tappable pill. Android gives every <see cref="Button"/> a minimum touch size of
/// about 48dp whatever it is asked for, which is what spilled ten of them across the scoreboard;
/// a border with a label and a tap is exactly as big as it is told to be.
/// </summary>
public class Chip : Border
{
    private readonly Label _label;

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(Chip), "", propertyChanged: (b, _, n) => ((Chip)b)._label.Text = (string)n);

    public static readonly BindableProperty IsSelectedProperty =
        BindableProperty.Create(nameof(IsSelected), typeof(bool), typeof(Chip), false, propertyChanged: (b, _, _) => ((Chip)b).Paint());

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>Anything the chip stands for — a speed, a tab, a tactic.</summary>
    public object? Tag { get; set; }

    public event EventHandler? Tapped;

    public static readonly Color Idle = Color.FromArgb("#2A3350");
    public static readonly Color Active = Color.FromArgb("#E94560");

    public Chip()
    {
        _label = new Label
        {
            FontSize = 11,
            FontFamily = "OpenSansSemibold",
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            // One line, always. Squeezed by its row, a label wrapped mid-word — "Balance / d",
            // "Pivo / t" — where it should only ever lose its tail.
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        };
        Content = _label;
        StrokeThickness = 0;
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 13 };
        Padding = new Thickness(10, 0);
        HeightRequest = 26;
        MinimumWidthRequest = 30;
        VerticalOptions = LayoutOptions.Center;
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => Tapped?.Invoke(this, EventArgs.Empty);
        GestureRecognizers.Add(tap);
        Paint();
    }

    /// <summary>The unselected colour. Transparent for a segment inside a control that has its own.</summary>
    public Color IdleColour { get => _idleColour; set { _idleColour = value; Paint(); } }
    private Color _idleColour = Idle;

    private void Paint() => BackgroundColor = IsSelected ? Active : _idleColour;
}
