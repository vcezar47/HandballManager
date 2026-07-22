using HandballManager.Services;

namespace HandballManager.Mobile.Controls;

/// <summary>
/// Renders <see cref="GameNotification"/>s as auto-dismissing toasts that slide in
/// at the top of the page. Drop one into a page's root grid as the last child so it
/// overlays the content; call <see cref="Attach"/> to bind it to the session's
/// notification service.
/// </summary>
public partial class ToastHost : ContentView
{
    private const int VisibleMilliseconds = 4000;
    private const int MaxSimultaneous = 3;

    private GameNotificationService? _service;
    private Action<string>? _onRouteTapped;

    public ToastHost()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Subscribes to a notification service. Safe to call repeatedly — the previous
    /// subscription is dropped first so re-appearing pages don't double-post.
    /// </summary>
    public void Attach(GameNotificationService service, Action<string>? onRouteTapped = null)
    {
        Detach();
        _service = service;
        _onRouteTapped = onRouteTapped;
        _service.Posted += OnPosted;
    }

    public void Detach()
    {
        if (_service != null) _service.Posted -= OnPosted;
        _service = null;
    }

    private void OnPosted(GameNotification notification)
    {
        // Notifications can be raised from simulation code running off the UI thread.
        MainThread.BeginInvokeOnMainThread(() => _ = ShowAsync(notification));
    }

    private async Task ShowAsync(GameNotification notification)
    {
        // Oldest first, so a burst during a long advance doesn't push the screen full.
        while (Stack.Children.Count >= MaxSimultaneous)
            Stack.Children.RemoveAt(0);

        var toast = BuildToast(notification);
        toast.Opacity = 0;
        toast.TranslationY = -24;
        Stack.Children.Add(toast);

        await Task.WhenAll(toast.FadeTo(1, 180, Easing.CubicOut),
                           toast.TranslateTo(0, 0, 180, Easing.CubicOut));

        await Task.Delay(VisibleMilliseconds);

        // A tap may have already removed it.
        if (!Stack.Children.Contains(toast)) return;

        await Task.WhenAll(toast.FadeTo(0, 200, Easing.CubicIn),
                           toast.TranslateTo(0, -24, 200, Easing.CubicIn));
        Stack.Children.Remove(toast);
    }

    private View BuildToast(GameNotification n)
    {
        var accent = AccentFor(n.Kind);

        var title = new Label
        {
            Text = n.Title,
            TextColor = accent,
            FontFamily = "OpenSansSemibold",
            FontSize = 13,
            CharacterSpacing = 1
        };

        var message = new Label
        {
            Text = n.Message,
            TextColor = Color.FromArgb("#EAEAEA"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var content = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(new GridLength(3)), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 10
        };
        content.Add(new BoxView { Color = accent, CornerRadius = 2 }, 0);
        content.Add(new VerticalStackLayout { Spacing = 2, Children = { title, message } }, 1);

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#16213E"),
            Stroke = new SolidColorBrush(Color.FromArgb("#22304F")),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 10),
            Content = content,
            InputTransparent = false
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            Stack.Children.Remove(border);
            if (n.Route != null) _onRouteTapped?.Invoke(n.Route);
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private static Color AccentFor(GameNotificationKind kind) => kind switch
    {
        GameNotificationKind.Trophy => Color.FromArgb("#F0A500"),
        GameNotificationKind.Transfer => Color.FromArgb("#E94560"),
        GameNotificationKind.YouthIntake => Color.FromArgb("#3DDC97"),
        GameNotificationKind.TransferWindow => Color.FromArgb("#4E9AF1"),
        _ => Color.FromArgb("#8888AA")
    };
}
