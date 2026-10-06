using HandballManager.Engine2D;
using HandballManager.Models;

namespace HandballManager.Mobile.Match2D;

/// <summary>The two settings of the empty-goal switch, as a row of chips.</summary>
public enum SeventhPlayer { Off, On }

/// <summary>A yes-or-no setting, as a row of chips.</summary>
public enum Switch { Off, On }

/// <summary>
/// The match on the 2D court. Landscape only for as long as it is open — the rest of the game
/// is a portrait app, and a handball court is twice as long as it is wide.
/// </summary>
public partial class Match2DPage : ContentPage
{
    private readonly Match2DViewModel _vm;
    private readonly PlayersDrawable _players;
    private readonly List<Chip> _speeds = [];
    private int _selectedCourtPlayer;

    public Match2DPage(Match2DViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        var theme = vm.Look.Theme;
        // The European weeks hang a portrait motif on every page; stretched across a landscape
        // screen it showed down both sides of the match. This page is the court's colour, edge to edge.
        BackgroundImageSource = null;
        BackgroundColor = Color.FromArgb(theme.Surround);
        Root.BackgroundColor = Color.FromArgb(theme.Surround);
        HomeStripe.Color = Color.FromArgb(vm.Look.Kits.Home.Shirt);
        AwayStripe.Color = Color.FromArgb(vm.Look.Kits.Away.Shirt);
        CourtLayer.Drawable = new CourtDrawable(theme);
        _players = new PlayersDrawable(vm);
        PlayersLayer.Drawable = _players;
        CentreLogo.Source = MauiImages.Get(vm.Look.Logo);

        CourtHost.SizeChanged += (_, _) => PlaceCrest();
        SizeChanged += (_, _) => FitToCutout();
        _vm.Frame += OnFrame;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Match2DViewModel.IsChoosingKeeper))
                UsualTakerChip.Text = _vm.IsChoosingKeeper ? "KEEP HER IN GOAL" : "USUAL TAKER";
            if (e.PropertyName is nameof(Match2DViewModel.IsPaused))
            {
                PauseChip.Text = _vm.IsPaused ? "▶" : "❚❚";
                PauseChip.IsSelected = _vm.IsPaused;
            }
            // The match changes its own speed for a time-out; the chips follow it.
            if (e.PropertyName is nameof(Match2DViewModel.Speed)) PaintSpeed();
            if (e.PropertyName is nameof(Match2DViewModel.TimeoutRequested)) TimeoutChip.IsSelected = _vm.TimeoutRequested;
        };

        // The controls.
        PauseChip.Tapped += (_, _) => _vm.TogglePauseCommand.Execute(null);
        foreach (var speed in new[] { 1, 2, 4, 16, 32 })
        {
            var chip = new Chip { Text = $"{speed}×", Tag = speed, Padding = new Thickness(9, 0), HeightRequest = 24 };
            chip.Tapped += (_, _) => SetSpeed(speed);
            _speeds.Add(chip);
            SpeedBar.Children.Add(chip);
        }
        TimeoutChip.Tapped += (_, _) => _vm.CallTimeoutCommand.Execute(null);
        SkipChip.Tapped += (_, _) => _vm.SkipToResultCommand.Execute(null);
        ViewChip.Tapped += (_, _) => ToggleView();
        SoundChip.Tapped += (_, _) => { _vm.ToggleSoundCommand.Execute(null); PaintSound(); };
        PaintSound();
        MenuChip.Tapped += (_, _) => ToggleDrawer();
        CloseDrawerChip.Tapped += (_, _) => ToggleDrawer();
        UsualTakerChip.Tapped += (_, _) => _vm.ChooseTaker(0);
        SecondHalfChip.Tapped += (_, _) => _vm.ContinueSecondHalfCommand.Execute(null);
        ContinueChip.Tapped += (_, _) => _vm.FinishCommand.Execute(null);

        foreach (var header in new[] { HomeHeader, AwayHeader })
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => FoldPanels(!_panelsFolded);
            header.GestureRecognizers.Add(tap);
        }
        _panelsFolded = Preferences.Get(PanelsFoldedKey, false);
        PaintFold();

        BuildTacticsPanel();
        SetSpeed(1);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        if (Platform.CurrentActivity is { } activity)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
            SetSystemBars(activity, visible: false);
        }
#endif
        // Once the screen has turned and the bars have gone: the insets are only final then.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(600), () =>
        {
            FitToCutout();
            _vm.OnShown();
        });
    }

    /// <summary>Width of a side panel's contents; the panel itself also covers any cut-out beside it.</summary>
    private const double PanelWidth = 144;

    /// <summary>Width of a folded panel: a column of shirts, each with its position under it.</summary>
    private const double FoldedPanelWidth = 36;

    private const string PanelsFoldedKey = "match2d.panels_folded";
    private bool _panelsFolded;
    private double _cutLeft, _cutRight;

    /// <summary>
    /// Folds both side panels down to their shirts, or opens them again. Folded, the court gets
    /// almost all of the screen's width; the choice is remembered for the next match.
    /// </summary>
    private void FoldPanels(bool folded)
    {
        _panelsFolded = folded;
        Preferences.Set(PanelsFoldedKey, folded);
        PaintFold();
    }

    private void PaintFold()
    {
        double width = _panelsFolded ? FoldedPanelWidth : PanelWidth;
        Root.ColumnDefinitions[0].Width = new GridLength(width + _cutLeft);
        Root.ColumnDefinitions[2].Width = new GridLength(width + _cutRight);
        var template = (DataTemplate)Resources[_panelsFolded ? "FoldedRowTemplate" : "SideRowTemplate"];
        HomePanel.ItemTemplate = template;
        AwayPanel.ItemTemplate = template;
        HomeHeaderName.IsVisible = AwayHeaderName.IsVisible = !_panelsFolded;
        // A button with a chevron that points the way the panel will move: towards the edge of
        // the screen to fold it, back towards the court to open it. Lit while folded, so the
        // way back is the most visible thing in the strip.
        HomeFoldGlyph.Text = _panelsFolded ? "›" : "‹";
        AwayFoldGlyph.Text = _panelsFolded ? "‹" : "›";
        HomeFoldButton.HorizontalOptions = _panelsFolded ? LayoutOptions.Center : LayoutOptions.End;
        AwayFoldButton.HorizontalOptions = _panelsFolded ? LayoutOptions.Center : LayoutOptions.Start;
        HomeFoldButton.BackgroundColor = AwayFoldButton.BackgroundColor = _panelsFolded ? Chip.Active : Chip.Idle;
    }

    /// <summary>
    /// The panels run out to the very edges of the screen, so the court between them is as big
    /// as the width allows. Their backgrounds go right up to the edge; only their contents, and
    /// the scoreboard, step in past a camera cut-out on whichever side it is.
    /// </summary>
    private void FitToCutout()
    {
        double left = 0, right = 0;
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(28)
            && Platform.CurrentActivity?.Window?.DecorView?.RootWindowInsets?.DisplayCutout is { } cut)
        {
            double density = DeviceDisplay.MainDisplayInfo.Density;
            if (density > 0)
            {
                left = Math.Max(left, cut.SafeInsetLeft / density);
                right = Math.Max(right, cut.SafeInsetRight / density);
            }
        }
#endif
        _cutLeft = left;
        _cutRight = right;
        PaintFold();
        HomePanelHost.Padding = new Thickness(1 + left, 3, 1, 3);
        AwayPanelHost.Padding = new Thickness(1, 3, 1 + right, 3);
        Scoreboard.Margin = new Thickness(4 + left, 0, 4 + right, 0);
    }

#if ANDROID
    /// <summary>
    /// The match gets the whole screen: the status and navigation bars hide, and a swipe from
    /// the edge brings them back for a moment, as in any full-screen game.
    /// </summary>
    private static void SetSystemBars(Android.App.Activity activity, bool visible)
    {
        var window = activity.Window;
        if (window == null) return;

        // Into the camera cut-out too: the page keeps its own margins clear of it.
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            var attrs = window.Attributes;
            if (attrs != null)
            {
                // Always rather than ShortEdges where there is one: a camera hole on the side of
                // a landscape screen still showed as a black strip down that edge.
                attrs.LayoutInDisplayCutoutMode = visible
                    ? Android.Views.LayoutInDisplayCutoutMode.Default
                    : OperatingSystem.IsAndroidVersionAtLeast(30)
                        ? Android.Views.LayoutInDisplayCutoutMode.Always
                        : Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
                window.Attributes = attrs;
            }
        }
        // Edge to edge while the match is on: without this the window still keeps the strips the
        // hidden bars would occupy, and they showed as black bands down both sides of the court.
        AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(window, visible);

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller == null) return;
        if (visible)
        {
            controller.Show(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
        }
        else
        {
            controller.SystemBarsBehavior = AndroidX.Core.View.WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            controller.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars());
        }
    }
#endif

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Frame -= OnFrame;
#if ANDROID
        if (Platform.CurrentActivity is { } activity)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
            SetSystemBars(activity, visible: true);
        }
#endif
    }

    // The match is finished (or skipped) from its own controls; back would orphan it mid-game.
    protected override bool OnBackButtonPressed() => true;

    private void OnFrame()
    {
        PlayersLayer.Invalidate();
        FastForward.Text = _vm.IsReplayingMoment ? "● KEY MOMENT" : "▶▶ FAST";
    }

    /// <summary>The crest sits in the throw-off area, wherever the court ends up on screen.</summary>
    private void PlaceCrest()
    {
        var t = new CourtTransform(new RectF(0, 0, (float)CourtHost.Width, (float)CourtHost.Height));
        var centre = t.Map(HandballManager.Engine2D.Court.CentreX, HandballManager.Engine2D.Court.CentreY);
        float size = t.Metres(3.7f);
        CentreLogo.WidthRequest = size;
        CentreLogo.HeightRequest = size;
        CentreLogo.TranslationX = centre.X - size / 2f;
        CentreLogo.TranslationY = centre.Y - size / 2f;
        CourtLayer.Invalidate();
        PlayersLayer.Invalidate();
    }

    private void SetSpeed(int speed)
    {
        _vm.SetSpeedCommand.Execute(speed.ToString());
        PaintSpeed();
    }

    private void PaintSpeed()
    {
        foreach (var chip in _speeds) chip.IsSelected = (int)chip.Tag! == _vm.Speed;
        FastForward.IsVisible = _vm.Speed >= 16;
    }

    private void PaintSound()
    {
        SoundChip.Text = _vm.IsSoundOn ? "🔊 SOUND" : "🔇 SOUND";
        SoundChip.IsSelected = _vm.IsSoundOn;
    }

    private void ToggleView()
    {
        bool text = !TextView.IsVisible;
        TextView.IsVisible = text;
        CourtLayer.IsVisible = PlayersLayer.IsVisible = CentreLogo.IsVisible = !text;
        ViewChip.Text = text ? "COURT VIEW" : "TEXT VIEW";
        ViewChip.IsSelected = text;
    }

    private void ToggleDrawer()
    {
        Drawer.IsVisible = !Drawer.IsVisible;
        MenuChip.IsSelected = Drawer.IsVisible;
    }

    // ── Substitutions, from the panel beside the court ─────────────────────────
    //
    // Tap one of your players, then another: one on the court and one on the bench, in either
    // order, and the change is made through the zone. Tapping the other side's panel does nothing.

    private SideRow? _picked;

    private async void OnRosterTapped(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is CollectionView cv) cv.SelectedItem = null;
        if (e.CurrentSelection.FirstOrDefault() is not SideRow row) return;
        bool mine = (_vm.UserSide == 0 ? _vm.HomeRoster : _vm.AwayRoster).Contains(row);
        if (!mine) return;

        if (_picked == null || _picked == row)
        {
            if (_picked != null) { _picked.Selected = false; _picked = null; _players.Highlighted = 0; return; }
            if (!row.OnCourt && !row.Available) return;
            _picked = row;
            row.Selected = true;
            _players.Highlighted = row.OnCourt ? row.Id : 0;
            PlayersLayer.Invalidate();
            return;
        }

        var first = _picked;
        first.Selected = false;
        _picked = null;
        _players.Highlighted = 0;

        var off = first.OnCourt ? first : row.OnCourt ? row : null;
        var on = !first.OnCourt ? first : !row.OnCourt ? row : null;
        if (off == null || on == null || !on.Available)
        {
            // Two on the court, or two on the bench: start again from the second one.
            if (row.OnCourt || row.Available) { _picked = row; row.Selected = true; _players.Highlighted = row.OnCourt ? row.Id : 0; }
            PlayersLayer.Invalidate();
            return;
        }

        if (!_vm.Substitute(off.Id, on.Id))
            await DisplayAlert("Substitution", "Not now — she has the ball, or a keeper can only replace a keeper.", "OK");
        PlayersLayer.Invalidate();
    }

    private void OnTakerSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SevenMetreCandidate row) return;
        TakerList.SelectedItem = null;
        _vm.ChooseTaker(row.Id);
    }

    // ── Tactics: rows of chips, applied from the next decision on ──────────────

    private void BuildTacticsPanel()
    {
        AddRow("TEMPO", [("Counter", AttackTempo.Counter), ("Balanced", AttackTempo.Balanced), ("Controlled", AttackTempo.Controlled)],
            () => _vm.Tempo, v => _vm.Tempo = v);
        AddRow("ATTACK THROUGH", [("Balanced", AttackFocus.Balanced), ("Wings", AttackFocus.Wings), ("Long shots", AttackFocus.LongShots),
                ("Pivot", AttackFocus.Pivot), ("Playmaker", AttackFocus.Playmaker)],
            () => _vm.Focus, v => _vm.Focus = v);
        AddRow("DEFENCE", [("6-0", DefensiveSystem.SixZero), ("5-1", DefensiveSystem.FiveOne), ("3-2-1", DefensiveSystem.ThreeTwoOne),
                ("Man-mark", DefensiveSystem.ManMarking)],
            () => _vm.DefenceSystem, v => _vm.DefenceSystem = v);
        AddRow("AGGRESSION", [("Contain", DefensiveAggression.Contain), ("Normal", DefensiveAggression.Normal), ("Aggressive", DefensiveAggression.Aggressive)],
            () => _vm.Aggression, v => _vm.Aggression = v);
        AddRow("7TH PLAYER", [("Keeper in goal", SeventhPlayer.Off), ("7 v 6 in attack", SeventhPlayer.On)],
            () => _vm.EmptyGoal ? SeventhPlayer.On : SeventhPlayer.Off, v => _vm.EmptyGoal = v == SeventhPlayer.On);
        AddRow("PLAY FOR TIME", [("No", Switch.Off), ("Run the clock down", Switch.On)],
            () => _vm.PlayForTime ? Switch.On : Switch.Off, v => _vm.PlayForTime = v == Switch.On);
        AddRow("SEVEN METRES", [("Ask me", Switch.On), ("Leave it to them", Switch.Off)],
            () => _vm.AsksForSevenMetres ? Switch.On : Switch.Off, v => _vm.AsksForSevenMetres = v == Switch.On);
        AddRow("SUBSTITUTIONS", [("Rotate tired players", Switch.On), ("Only mine", Switch.Off)],
            () => _vm.AutoSubstitutions ? Switch.On : Switch.Off, v => _vm.AutoSubstitutions = v == Switch.On);
    }

    /// <summary>
    /// One setting as a segmented control: equal segments in a dark track, the chosen one lit.
    /// Up to four sit in one line; five go three and two. A wrapping row used to squeeze each
    /// chip to whatever was left of its line, and the labels broke mid-word.
    /// </summary>
    private void AddRow<T>(string title, (string Label, T Value)[] options, Func<T> get, Action<T> set) where T : struct, Enum
    {
        TacticsPanel.Children.Add(new Label
        {
            Text = title, TextColor = Color.FromArgb("#8892B0"), FontSize = 9, FontFamily = "OpenSansSemibold",
            CharacterSpacing = 1.5, Margin = new Thickness(2, 10, 0, 4),
        });

        int columns = options.Length <= 4 ? options.Length : 3;
        var grid = new Grid { ColumnSpacing = 2, RowSpacing = 2 };
        for (int c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r < (options.Length + columns - 1) / columns; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var chips = new List<(Chip, T)>();
        for (int i = 0; i < options.Length; i++)
        {
            var (label, value) = options[i];
            var chip = new Chip
            {
                Text = label, IdleColour = Colors.Transparent, Padding = new Thickness(4, 0), HeightRequest = 28,
                HorizontalOptions = LayoutOptions.Fill, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            };
            chip.Tapped += (_, _) => { set(value); Paint(); };
            chips.Add((chip, value));
            grid.Add(chip, i % columns, i / columns);
        }
        TacticsPanel.Children.Add(new Border
        {
            Content = grid, BackgroundColor = Color.FromArgb("#1E2640"), StrokeThickness = 0, Padding = 2,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
        });
        Paint();

        void Paint()
        {
            foreach (var (chip, v) in chips) chip.IsSelected = EqualityComparer<T>.Default.Equals(v, get());
        }
    }
}
