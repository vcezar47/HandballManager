using System.ComponentModel;
using HandballManager.Engine2D;
using HandballManager.Localization;
using Microsoft.Maui.Controls.Shapes;

namespace HandballManager.Mobile.Match2D;

/// <summary>
/// The match's scoreboard in its competition's own style: the EHF broadcast board for European
/// nights, and a board in each league's, cup's and supercup's colours for everything else.
/// </summary>
/// <remarks>
/// <para>
/// Built in code rather than XAML because the four shapes share their parts (a team's kit band,
/// its code, a score box, the clock) but arrange them differently, and four templates kept in
/// step by hand would drift. The style comes from <see cref="ScoreboardStyle"/> in the core, so
/// the colours are testable without a device.
/// </para>
/// <para>
/// Every shape is a single row, so the board sits in the middle of the top bar with the buttons
/// either side. The kit bands are the colours each side wears in this match (change kit
/// included), shirt over the second colour, running the full height of the board.
/// </para>
/// </remarks>
public sealed class ScoreboardView : ContentView
{
    /// <summary>Barlow Condensed: narrow, as broadcast boards are, so three letters and two scores fit a small bar.</summary>
    private const string Font = "BarlowCondensed";
    private const double Height = 30;

    private readonly Match2DViewModel _vm;
    private readonly ScoreboardStyle _style;

    private readonly Label _homeScore = Score(), _awayScore = Score();
    private readonly Label _clock = new() { FontFamily = Font, FontSize = 17, VerticalTextAlignment = TextAlignment.Center };
    private readonly Border _homeSuspension, _awaySuspension, _emptyGoal;
    private readonly Label _homeSuspensionText, _awaySuspensionText;

    /// <summary>The break board: full names behind the codes, a caption in place of the clock, no timers.</summary>
    private readonly bool _expanded;
    private string _caption = "";
    private readonly List<(int Side, Grid Slot, Label Code, Label Name)> _slots = [];

    /// <param name="expanded">
    /// The board the half-time and full-time screen raises: the same style, its codes ready to
    /// open into the clubs' full names (<see cref="Open"/>, <see cref="RevealNamesAsync"/>), and a caption where the clock
    /// would be.
    /// </param>
    public ScoreboardView(Match2DViewModel vm, bool expanded = false)
    {
        _vm = vm;
        _style = vm.Look.Board;
        _expanded = expanded;

        (_homeSuspension, _homeSuspensionText) = SuspensionBox();
        (_awaySuspension, _awaySuspensionText) = SuspensionBox();
        _emptyGoal = EmptyGoalTag();

        var board = _style.Shape switch
        {
            ScoreboardShape.Broadcast => Broadcast(),
            ScoreboardShape.Ribbon => Ribbon(),
            ScoreboardShape.Angular => Angular(),
            _ => Tabs(),
        };

        Content = expanded
            ? board
            : new HorizontalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children = { _homeSuspension, board, _awaySuspension },
            };

        if (expanded)
        {
            _emptyGoal.IsVisible = false;
            _homeSuspension.IsVisible = _awaySuspension.IsVisible = false;
        }

        Refresh();
        vm.PropertyChanged += OnVmChanged;
    }

    /// <summary>Stops listening to the match: the page calls this when it goes.</summary>
    public void Detach() => _vm.PropertyChanged -= OnVmChanged;

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Match2DViewModel.HomeScore) or nameof(Match2DViewModel.AwayScore)
            or nameof(Match2DViewModel.ClockText) or nameof(Match2DViewModel.HalfText)
            or nameof(Match2DViewModel.HomeSuspension) or nameof(Match2DViewModel.AwaySuspension)
            or nameof(Match2DViewModel.HomeEmptyGoal) or nameof(Match2DViewModel.AwayEmptyGoal))
        {
            if (MainThread.IsMainThread) Refresh();
            else MainThread.BeginInvokeOnMainThread(Refresh);
        }
    }

    private void Refresh()
    {
        _homeScore.Text = _vm.HomeScore.ToString();
        _awayScore.Text = _vm.AwayScore.ToString();
        _clock.Text = _expanded ? _caption
            : _style.ShowsHalf && _vm.HalfText.Length > 0 ? $"{_vm.HalfText}  {_vm.ClockText}" : _vm.ClockText;
        if (_expanded) return;

        _homeSuspension.IsVisible = _vm.HomeSuspension.Length > 0;
        _homeSuspensionText.Text = _vm.HomeSuspension;
        _awaySuspension.IsVisible = _vm.AwaySuspension.Length > 0;
        _awaySuspensionText.Text = _vm.AwaySuspension;
        _emptyGoal.IsVisible = _vm.HomeEmptyGoal || _vm.AwayEmptyGoal;
    }

    // ─── The shapes ─────────────────────────────────────────────────────────────

    /// <summary>The EHF board: one dark bar, the crest between the scores, the clock in a tab beneath.</summary>
    private View Broadcast()
    {
        Paint(_homeScore, "#FFFFFF");
        Paint(_awayScore, "#FFFFFF");
        _homeScore.FontSize = _awayScore.FontSize = 24;

        var bar = Row(_style.Bar,
            KitBand(Home), Pad(Name(Home, "#FFFFFF"), 8, 4), Pad(_homeScore, 2, 4),
            Pad(Crest(26), 4, 4), Pad(_awayScore, 4, 2), Pad(Name(Away, "#FFFFFF"), 4, 8), KitBand(Away));
        bar.HeightRequest = 32;

        _clock.TextColor = Color.FromArgb(_style.OnPrimary);
        var tab = new Border
        {
            BackgroundColor = Color.FromArgb(_style.Primary), StrokeThickness = 0, Padding = new Thickness(14, 0),
            HeightRequest = 20, Content = _clock,
        };

        var under = new HorizontalStackLayout { HorizontalOptions = LayoutOptions.Center, Children = { tab, _emptyGoal } };
        if (TagBox() is { } tag) under.Children.Add(tag);

        return new VerticalStackLayout { Spacing = 0, Children = { bar, under } };
    }

    /// <summary>Blocks side by side: crest, codes on the dark ground, scores in accent boxes, clock.</summary>
    private View Tabs()
    {
        Paint(_homeScore, _style.OnAccent);
        Paint(_awayScore, _style.OnAccent);
        _clock.TextColor = Color.FromArgb(_style.OnPrimary);

        var row = Row(null,
            Block(_style.Primary, Crest(22), 6),
            KitBand(Home),
            Block(_style.Bar, Name(Home, "#FFFFFF"), 8),
            ScoreBox(_homeScore),
            new BoxView { Color = Color.FromArgb(_style.Bar), WidthRequest = 2 },
            ScoreBox(_awayScore),
            Block(_style.Bar, Name(Away, "#FFFFFF"), 8),
            KitBand(Away),
            Block(_style.Primary, _clock, 9));
        row.Children.Add(_emptyGoal);
        return TagBelow(Trim(row));
    }

    /// <summary>A light ribbon: a coloured crest block, dark codes, the scores in the competition's colour.</summary>
    private View Ribbon()
    {
        Paint(_homeScore, _style.Primary);
        Paint(_awayScore, _style.Primary);
        _clock.TextColor = Color.FromArgb(_style.OnAccent);

        var row = Row(_style.Bar,
            Block(_style.Primary, Crest(22), 6),
            KitBand(Home),
            Pad(Name(Home, "#15171C"), 8, 6), Pad(_homeScore, 0, 4),
            Pad(new Label { Text = "–", TextColor = Color.FromArgb("#9AA0AA"), FontSize = 14, VerticalTextAlignment = TextAlignment.Center }, 0, 0),
            Pad(_awayScore, 4, 0), Pad(Name(Away, "#15171C"), 6, 8),
            KitBand(Away),
            Block(_style.Accent, _clock, 9));
        row.Children.Add(_emptyGoal);
        return TagBelow(Trim(row));
    }

    /// <summary>Slanted blocks: crest, then the teams and scores, then the clock.</summary>
    private View Angular()
    {
        Paint(_homeScore, _style.OnAccent);
        Paint(_awayScore, _style.OnAccent);
        _clock.TextColor = Color.FromArgb(_style.OnPrimary);

        var teams = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                KitBand(Home), Name(Home, "#FFFFFF"),
                AccentBox(_homeScore), AccentBox(_awayScore),
                Name(Away, "#FFFFFF"), KitBand(Away),
            },
        };

        var row = new HorizontalStackLayout
        {
            Spacing = 0,
            HeightRequest = Height,
            Children =
            {
                Slanted(_style.Primary, Crest(22), left: false, right: true),
                Slanted(_style.Bar, teams, left: true, right: true, overlap: true),
                Slanted(_style.Primary, _clock, left: true, right: false, overlap: true),
            },
        };
        row.Children.Add(_emptyGoal);
        return TagBelow(Trim(row));
    }

    // ─── The parts ──────────────────────────────────────────────────────────────

    private const int Home = 0, Away = 1;

    private static Label Score() => new()
    {
        FontFamily = Font, FontSize = 22, HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center, MinimumWidthRequest = 22,
    };

    private static void Paint(Label label, string hex) => label.TextColor = Color.FromArgb(hex);

    /// <summary>
    /// A side's name on the board: its code, or on the break board a slot holding the code with
    /// the full name behind it, ready to unfold.
    /// </summary>
    private View Name(int side, string hex)
    {
        string code = side == Home ? _vm.TeamCodes.Home : _vm.TeamCodes.Away;
        if (!_expanded) return Code(code, hex);

        var codeLabel = Code(code, hex);
        var nameLabel = Code(side == Home ? _vm.HomeTeamName : _vm.AwayTeamName, hex);
        nameLabel.Opacity = 0;
        nameLabel.LineBreakMode = LineBreakMode.TailTruncation;
        // Code and name both sit against their score, so the name grows out of the code towards
        // the board's edge, and a short name leaves its room there rather than beside the score.
        codeLabel.HorizontalTextAlignment = nameLabel.HorizontalTextAlignment =
            side == Home ? TextAlignment.End : TextAlignment.Start;
        var slot = new Grid { Children = { codeLabel, nameLabel }, VerticalOptions = LayoutOptions.Fill };
        _slots.Add((side, slot, codeLabel, nameLabel));
        return slot;
    }

    /// <summary>Sets what the break board says where its clock would be.</summary>
    public void SetCaption(string caption)
    {
        _caption = caption;
        _clock.Text = caption;
    }

    /// <summary>The caption, for the break screen to fade in on its own beat.</summary>
    public Label Caption => _clock;

    /// <summary>The names' type size before any shrinking to fit.</summary>
    private const double NameSize = 19, SmallestNameSize = 13;

    /// <summary>
    /// The width each side's slot takes once the board is open: both the same, so the scores sit
    /// on the board's own middle line between them.
    /// </summary>
    private double NameWidth() => _slots.Max(s => s.Name.Measure(double.PositiveInfinity, Height).Width) + 4;

    private double SlotNow(int side) => _slots.FirstOrDefault(s => s.Side == side).Slot?.Width ?? 0;

    /// <summary>
    /// How wide the board is with both slots at <paramref name="slot"/>. Worked out from the
    /// board as it is laid out now, whatever its slots are: each holds its club's full name,
    /// unseen, from the start, so before opening they are as unequal as the names.
    /// </summary>
    private double WidthWithSlots(double slot) =>
        Content is VisualElement board ? board.Width + 2 * slot - SlotNow(Home) - SlotNow(Away) : 0;

    /// <summary>How wide the board will be open, with the names at their own size.</summary>
    public double OpenWidth() => _slots.Count > 0 ? WidthWithSlots(NameWidth()) : Content?.Width ?? 0;

    /// <summary>
    /// How far the open board must shift for the middle of its two scores to sit on the screen's
    /// centre. A shape with a crest block at one end and the clock at the other is not
    /// symmetrical, and the break screen holds the scores, not the board, in the middle.
    /// </summary>
    public double CentreOffset()
    {
        if (Content is not VisualElement board || board.Width <= 0) return 0;
        // As it will be once both slots are equal. Opening the home slot moves the scores right;
        // opening the away one only widens the board. Read from the layout as it is now.
        double home = XWithin(_homeScore, board), away = XWithin(_awayScore, board) + _awayScore.Width;
        return board.Width / 2 - (home + away) / 2 + (SlotNow(Home) - SlotNow(Away)) / 2;
    }

    /// <summary>
    /// Sizes the board open, before it is seen, no wider than <paramref name="maxWidth"/>: both
    /// slots to the longer name's own width when there is room; when there is not, both names set
    /// smaller, down to <see cref="SmallestNameSize"/>, and only past that cut short. Done in one
    /// step rather than animated: a width that changes every frame lays the whole match page out
    /// again every frame, benches and all, and that is what made the opening stutter.
    /// </summary>
    public void Open(double maxWidth)
    {
        if (Content is not VisualElement board || board.Width <= 0 || _slots.Count == 0) return;

        double need = NameWidth();
        double room = (maxWidth - board.Width + SlotNow(Home) + SlotNow(Away)) / 2;
        if (need > room && room > 0)
        {
            double size = Math.Max(SmallestNameSize, NameSize * room / need);
            foreach (var s in _slots) s.Name.FontSize = size;
            need = NameWidth();
        }
        double slot = Math.Max(_slots.Max(s => s.Code.Width), room > 0 ? Math.Min(need, room) : need);
        foreach (var s in _slots) s.Slot.WidthRequest = slot;
    }

    /// <summary>
    /// The codes open into the clubs' full names: each code fades as its name comes up out of it,
    /// sliding outward from the score. Opacity and translation only, which the device draws
    /// without laying anything out again.
    /// </summary>
    public Task RevealNamesAsync(uint length = 700)
    {
        if (_slots.Count == 0) return Task.CompletedTask;
        var all = new List<Task>();
        foreach (var s in _slots)
        {
            s.Name.TranslationX = s.Side == Home ? 14 : -14;
            all.Add(s.Code.FadeToAsync(0, length / 2, Easing.CubicIn));
            all.Add(s.Name.FadeToAsync(1, length, Easing.CubicOut));
            all.Add(s.Name.TranslateToAsync(0, 0, length, Easing.CubicOut));
        }
        return Task.WhenAll(all);
    }

    private static double XWithin(VisualElement element, VisualElement ancestor)
    {
        double x = 0;
        for (Element? e = element; e != null && e != ancestor; e = e.Parent)
            if (e is VisualElement v) x += v.Frame.X;
        return x;
    }

    private static Label Code(string code, string hex) => new()
    {
        Text = code, FontFamily = Font, FontSize = 19, TextColor = Color.FromArgb(hex),
        VerticalTextAlignment = TextAlignment.Center, CharacterSpacing = 0.5,
    };

    /// <summary>The colours a side wears in this match, shirt over its second colour, the board's full height.</summary>
    private View KitBand(int side)
    {
        var kit = side == Home ? _vm.Look.Kits.Home : _vm.Look.Kits.Away;
        var grid = new Grid { WidthRequest = 6, RowDefinitions = [new(GridLength.Star), new(GridLength.Star)], RowSpacing = 0 };
        grid.Add(new BoxView { Color = Color.FromArgb(kit.Shirt) }, 0, 0);
        grid.Add(new BoxView { Color = Color.FromArgb(kit.Number) }, 0, 1);
        return grid;
    }

    private Image Crest(double size) => new()
    {
        Source = MauiImages.Get(_vm.Look.BoardCrest), HeightRequest = size, WidthRequest = size,
        Aspect = Aspect.AspectFit, VerticalOptions = LayoutOptions.Center,
    };

    private static HorizontalStackLayout Row(string? background, params View[] parts)
    {
        var row = new HorizontalStackLayout { Spacing = 0, HeightRequest = Height };
        if (background != null) row.BackgroundColor = Color.FromArgb(background);
        foreach (var p in parts) row.Children.Add(p);
        return row;
    }

    private static View Pad(View v, double left, double right)
    {
        v.Margin = new Thickness(left, 0, right, 0);
        v.VerticalOptions = LayoutOptions.Center;
        return v;
    }

    private static Border Block(string hex, View content, double padding) => new()
    {
        BackgroundColor = Color.FromArgb(hex), StrokeThickness = 0, Padding = new Thickness(padding, 0), Content = content,
    };

    private Border ScoreBox(Label score) => new()
    {
        BackgroundColor = Color.FromArgb(_style.Accent), StrokeThickness = 0, WidthRequest = 28, Content = score,
    };

    private Border AccentBox(Label score) => new()
    {
        BackgroundColor = Color.FromArgb(_style.Accent), StrokeThickness = 0, Padding = new Thickness(5, 0),
        HeightRequest = 24, VerticalOptions = LayoutOptions.Center, Content = score,
    };

    /// <summary>"SEMI-FINAL", "SUPERCUP": gold on a supercup, the accent on a cup.</summary>
    private Border? TagBox()
    {
        if (_style.Tag is not { } text) return null;
        bool gold = _style.Gold;
        return new Border
        {
            BackgroundColor = Color.FromArgb(gold ? ScoreboardStyle.GoldColour : _style.Accent),
            StrokeThickness = 0, Padding = new Thickness(7, 0), VerticalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = text, FontFamily = Font, FontSize = 13, VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb(gold ? ScoreboardStyle.OnGold : _style.OnAccent),
            },
        };
    }

    /// <summary>
    /// "SUPERCUP · SEMI-FINAL" hung under the board's right end, below the clock, rather than
    /// carried at the end of the row: there it made the longest shapes wider than the space
    /// between the top bar's buttons, and on the break board it ran off the screen.
    /// </summary>
    private View TagBelow(View board)
    {
        if (TagBox() is not { } tag) return board;
        tag.HorizontalOptions = LayoutOptions.End;
        tag.HeightRequest = 18;
        return new VerticalStackLayout { Spacing = 0, Children = { board, tag } };
    }

    /// <summary>A supercup's board, trimmed in gold; anything else as it is.</summary>
    private View Trim(View board) => _style.Gold
        ? new Border { Content = board, Stroke = Color.FromArgb(ScoreboardStyle.GoldColour), StrokeThickness = 2, Padding = 0 }
        : board;

    private static (Border Box, Label Text) SuspensionBox()
    {
        var text = new Label { FontFamily = Font, FontSize = 15, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center };
        var box = new Border
        {
            BackgroundColor = Color.FromArgb(ScoreboardStyle.SuspensionColour), StrokeThickness = 0,
            Padding = new Thickness(5, 1), VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, 4, 0, 0),
            Content = text, IsVisible = false,
        };
        return (box, text);
    }

    private static Border EmptyGoalTag() => new()
    {
        BackgroundColor = Color.FromArgb("#DADADA"), StrokeThickness = 0, Padding = new Thickness(5, 0), IsVisible = false,
        Content = new Label
        {
            Text = Loc.T("EMPTY GOAL"), FontFamily = Font, FontSize = 11, LineHeight = 0.85, MaximumWidthRequest = 40,
            TextColor = Color.FromArgb("#0C1733"), HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap,
        },
    };

    /// <summary>
    /// A block with slanted ends, the angle redrawn once the block knows its size. Overlapping
    /// blocks tuck under the previous one's slant, so the joins read as one cut.
    /// </summary>
    private static Border Slanted(string hex, View content, bool left, bool right, bool overlap = false)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb(hex), StrokeThickness = 0, Content = content,
            Padding = new Thickness(left ? 14 : 9, 0, right ? 14 : 9, 0),
            Margin = new Thickness(overlap ? -6 : 0, 0, 0, 0),
            HeightRequest = Height,
        };
        if (content is Label l) l.VerticalTextAlignment = TextAlignment.Center;
        content.VerticalOptions = LayoutOptions.Center;

        border.SizeChanged += (_, _) =>
        {
            double w = border.Width, h = border.Height, k = h * 0.3;
            if (w <= 0 || h <= 0) return;
            border.StrokeShape = new Polygon
            {
                Points = [new(left ? k : 0, 0), new(w, 0), new(right ? w - k : w, h), new(0, h)],
            };
        };
        return border;
    }
}
