namespace HandballManager.Mobile.Infrastructure;

/// <summary>
/// Builds the two-column attribute grids used by the player and youth detail pages,
/// with values colour-coded by strength (1–20 scale).
/// </summary>
public static class AttributeGridBuilder
{
    public static View Build(IReadOnlyList<(string Label, int Value)> attributes)
        => Build(attributes.Select(a => (a.Label, a.Value.ToString())).ToList());

    /// <summary>
    /// String-valued variant used with MaskedPlayerProxy — unscouted players show
    /// estimation ranges like "8–14" which render dimmed.
    /// </summary>
    public static View Build(IReadOnlyList<(string Label, string Value)> attributes)
    {
        var grid = new Grid
        {
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(30)),
                new ColumnDefinition(new GridLength(18)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(30)),
            },
        };

        int rows = (attributes.Count + 1) / 2;
        for (int r = 0; r < rows; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < attributes.Count; i++)
        {
            var (label, value) = attributes[i];
            int row = i / 2;
            int colBase = (i % 2) * 3;

            grid.Add(new Label
            {
                Text = label,
                TextColor = Color.FromArgb("#8888AA"),
                FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center,
            }, colBase, row);

            grid.Add(new Label
            {
                Text = value,
                TextColor = int.TryParse(value, out int v) ? ValueColor(v) : Color.FromArgb("#8888AA"),
                FontSize = int.TryParse(value, out _) ? 14 : 11,
                FontFamily = "OpenSansSemibold",
                HorizontalTextAlignment = TextAlignment.End,
                LineBreakMode = LineBreakMode.NoWrap,
                VerticalOptions = LayoutOptions.Center,
            }, colBase + 1, row);
        }

        return grid;
    }

    private static Color ValueColor(int value) => value switch
    {
        >= 16 => Color.FromArgb("#6EE7A0"),
        >= 12 => Color.FromArgb("#F0A500"),
        >= 8 => Color.FromArgb("#EAEAEA"),
        _ => Color.FromArgb("#8888AA"),
    };
}
