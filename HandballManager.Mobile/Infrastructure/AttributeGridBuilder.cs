namespace HandballManager.Mobile.Infrastructure;

/// <summary>
/// Builds the two-column attribute grids used by the player, youth and manager detail
/// pages, with values colour-coded by strength (1–20 scale) and an optional arrow showing
/// how far the attribute has moved this season.
/// </summary>
public static class AttributeGridBuilder
{
    public static View Build(IReadOnlyList<(string Label, int Value)> attributes)
        => Build(attributes.Select(a => (a.Label, a.Value.ToString(), 0)).ToList());

    /// <summary>
    /// String-valued variant used with MaskedPlayerProxy — unscouted players show
    /// estimation ranges like "8–14" which render dimmed.
    /// </summary>
    public static View Build(IReadOnlyList<(string Label, string Value)> attributes)
        => Build(attributes.Select(a => (a.Label, a.Value, 0)).ToList());

    /// <param name="attributes">
    /// Label, display value, and this season's net change in that attribute — 0 for no
    /// arrow. The change comes from <c>Player.SeasonAttributeChanges</c>.
    /// </param>
    public static View Build(IReadOnlyList<(string Label, string Value, int Change)> attributes)
    {
        var grid = new Grid
        {
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(46)),
                new ColumnDefinition(new GridLength(18)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(46)),
            },
        };

        int rows = (attributes.Count + 1) / 2;
        for (int r = 0; r < rows; r++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < attributes.Count; i++)
        {
            var (label, value, change) = attributes[i];
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

            var cell = new HorizontalStackLayout
            {
                Spacing = 3,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
            };

            // No arrow next to an estimation range: if the player is not scouted well
            // enough to show a number, their progression is not ours to reveal either.
            if (int.TryParse(value, out _) && TrendIcon(change) is { } icon)
            {
                cell.Add(new Image
                {
                    Source = MauiImages.Get(icon),
                    WidthRequest = 11,
                    HeightRequest = 11,
                    Aspect = Aspect.AspectFit,
                    VerticalOptions = LayoutOptions.Center,
                });
            }

            cell.Add(new Label
            {
                Text = value,
                TextColor = int.TryParse(value, out int v) ? ValueColor(v) : Color.FromArgb("#8888AA"),
                FontSize = int.TryParse(value, out _) ? 14 : 11,
                FontFamily = "OpenSansSemibold",
                HorizontalTextAlignment = TextAlignment.End,
                LineBreakMode = LineBreakMode.NoWrap,
                VerticalOptions = LayoutOptions.Center,
            });

            grid.Add(cell, colBase + 1, row);
        }

        return grid;
    }

    /// <summary>Steep arrow for a big move, shallow for a single point. Matches the desktop art.</summary>
    private static string? TrendIcon(int change) => change switch
    {
        >= 2 => "prog90.png",
        1 => "prog45.png",
        -1 => "reg45.png",
        <= -2 => "reg90.png",
        _ => null
    };

    private static Color ValueColor(int value) => value switch
    {
        >= 16 => Color.FromArgb("#6EE7A0"),
        >= 12 => Color.FromArgb("#F0A500"),
        >= 8 => Color.FromArgb("#EAEAEA"),
        _ => Color.FromArgb("#8888AA"),
    };
}
