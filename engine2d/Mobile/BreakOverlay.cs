using HandballManager.Engine2D;
using HandballManager.Localization;

namespace HandballManager.Mobile.Match2D;

/// <summary>
/// The break screen: half-time, the break before extra time, and full time. The court dims
/// slowly, the competition's own scoreboard rises into the middle, its codes open out into the
/// clubs' full names, the caption comes up where the clock would be, and then the way on.
/// </summary>
/// <remarks>
/// <para>
/// Paced on purpose. Behind the full-time screen the rest of the matchday is already being
/// simulated (<c>GameSession.FileWatchedMatch</c>), so the seconds this takes are seconds that
/// work would otherwise have made the player wait through after CONTINUE.
/// </para>
/// <para>
/// The scores hold the middle of the screen throughout: the names open to the same width on both
/// sides, and a shape that is not symmetrical (a crest block at one end, the clock at the other)
/// is shifted so its two scores, not its outline, are centred. The board is enlarged only as far
/// as the screen allows once the names are open, and long names are set smaller before they are
/// ever cut, so the whole board stays on the screen.
/// </para>
/// <para>
/// The screen is put into the page when a break starts and taken out when play resumes, so
/// nothing of it is in the page during play. Each part fades up from nothing once it is in.
/// </para>
/// <para>
/// The dim is this screen's own background colour, faded from clear, not a box faded from
/// opacity 0. On the tester's phone a box left at opacity 0, never yet animated, is drawn solid
/// light grey: the half-second hold at the whistle showed as a grey flash, and in 76, where the
/// box sat there from kick-off, the whole match was grey. A colour's own transparency is drawn
/// the same everywhere, as the skip's dark cover always has been.
/// </para>
/// <para>
/// The timeline is in real time, the same at 1× and 32×; it opens with a short hold on the last
/// frame so the whistle lands before the court starts to dim.
/// </para>
/// </remarks>
public sealed class BreakOverlay : ContentView
{
    /// <summary>How much larger than the top bar's board the break board is drawn, when it fits.</summary>
    private const double LargestScale = 1.5;
    /// <summary>The space kept clear either side of the board.</summary>
    private const double SideMargin = 24;

    /// <summary>The court, dimmed: not to black, it stays readable underneath.</summary>
    private static readonly Color DimColour = Color.FromArgb("#05080F");
    private const float DimTo = 0.62f;

    private readonly ContentView _boardHost = new() { HorizontalOptions = LayoutOptions.Center };
    private readonly Chip _continue = new() { IsSelected = true, HeightRequest = 34, HorizontalOptions = LayoutOptions.Center, Padding = new Thickness(22, 0) };

    private readonly Grid _host;
    private ScoreboardView? _board;
    private Action? _onContinue;
    private int _showing;   // which showing is current, so a superseded animation stops quietly
    private bool _up;

    /// <param name="host">
    /// The page's root grid. The screen goes in just under its last child, the skip's own cover,
    /// for as long as a break lasts.
    /// </param>
    public BreakOverlay(Grid host)
    {
        _host = host;
        BackgroundColor = Colors.Transparent;
        _continue.Opacity = 0;
        _continue.Tapped += (_, _) =>
        {
            if (_continue.Opacity < 0.5) return;   // not offered yet
            // Not cleared after one tap: each command guards itself against a second, and an
            // ad that came back without starting the half must leave a way on.
            _onContinue?.Invoke();
        };

        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 34,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { _boardHost, _continue },
                },
            },
        };
    }

    public bool IsShowing => _up;

    /// <summary>
    /// Raises the break screen for <paramref name="caption"/>. Calling it again while it is up
    /// only updates the caption and what CONTINUE does.
    /// </summary>
    public async Task ShowAsync(Match2DViewModel vm, string caption, string continueText, Action onContinue)
    {
        _onContinue = onContinue;
        _continue.Text = continueText;
        if (_up && _board != null)
        {
            _board.SetCaption(caption);
            return;
        }

        int showing = ++_showing;
        _up = true;

        _board?.Detach();
        _board = new ScoreboardView(vm, expanded: true) { Opacity = 0, TranslationY = 18 };
        _board.SetCaption(caption);
        _board.Caption.Opacity = 0;
        _boardHost.Content = _board;
        BackgroundColor = Colors.Transparent;
        _continue.Opacity = 0;
        if (Parent == null) _host.Children.Insert(Math.Max(0, _host.Children.Count - 1), this);

        // The last frame holds a moment, then the court dims — slowly, and not to black: it
        // stays readable underneath.
        await Task.Delay(500);
        if (showing != _showing) return;
        new Animation(a => BackgroundColor = DimColour.WithAlpha((float)a), 0, DimTo, Easing.CubicInOut)
            .Commit(this, "dim", length: 1600);

        await Task.Delay(1000);
        if (showing != _showing) return;

        // Sized before it is seen, so it neither slides nor grows past the screen once it is up:
        // as large as the screen allows with the names open, the scores on the screen's centre.
        double room = Math.Max(0, Width - 2 * SideMargin);
        // Read before the slots are resized: both are worked out from the layout as it stands.
        // The shift that centres the scores counts against the room as well, on both sides,
        // or a lopsided board that fits is pushed off one edge.
        double open = _board.OpenWidth(), offset = _board.CentreOffset();
        double scale = Math.Clamp(room / Math.Max(1, open + 2 * Math.Abs(offset)), 1, LargestScale);
        _board.Open(room / scale - 2 * Math.Abs(offset));
        _board.Scale = scale;
        _board.TranslationX = offset * scale;
        Infrastructure.AppLog.Info($"break board: room {room:F0}, laid out {_board.Content?.Width:F0}, open {open:F0}, offset {offset:F0}, scale {scale:F2}");
        await Task.WhenAll(
            _board.FadeToAsync(1, 700, Easing.CubicOut),
            _board.TranslateToAsync(_board.TranslationX, 0, 700, Easing.CubicOut));

        await Task.Delay(150);
        if (showing != _showing) return;
        await _board.RevealNamesAsync(700);

        if (showing != _showing) return;
        await _board.Caption.FadeToAsync(1, 600, Easing.CubicOut);

        await Task.Delay(100);
        if (showing != _showing) return;
        await _continue.FadeToAsync(1, 500, Easing.CubicOut);
    }

    /// <summary>Takes the screen down, at once.</summary>
    public void Hide()
    {
        if (!_up) return;
        _up = false;
        _showing++;
        this.AbortAnimation("dim");
        _continue.CancelAnimations();
        _board?.Detach();
        _board = null;
        _boardHost.Content = null;
        _onContinue = null;
        BackgroundColor = Colors.Transparent;
        _continue.Opacity = 0;
        _host.Children.Remove(this);
    }

    /// <summary>The words on CONTINUE for each break.</summary>
    public static string ContinueFor(bool halfTime) => halfTime ? Loc.T("SECOND HALF") : Loc.T("CONTINUE");
}
