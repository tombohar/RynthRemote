namespace RynthRemote.Controls;

/// Full-screen native HD video (modal). Hosts the HdVideoView (iOS: AVSampleBufferDisplayLayer fed by
/// the agent's WebSocket H.264) with a close button overlaid. Full-screen sidesteps the native-view-over-
/// WebView scroll-sync problem and is the better posture for actually playing the character anyway.
public sealed class HdVideoPage : ContentPage
{
    public HdVideoPage(string baseUrl, int pid, string token, string title)
    {
        BackgroundColor = Colors.Black;
        NavigationPage.SetHasNavigationBar(this, false);

        var video = new HdVideoView { BaseUrl = baseUrl, Pid = pid, Token = token };

        var close = new Button
        {
            Text = "✕",
            FontSize = 20,
            TextColor = Colors.White,
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.55),
            CornerRadius = 22,
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 50, 16, 0),
        };
        close.Clicked += async (_, _) => { try { await Navigation.PopModalAsync(); } catch { } };

        var label = new Label
        {
            Text = title,
            TextColor = Color.FromRgba(1, 1, 1, 0.7),
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(16, 58, 0, 0),
        };

        Content = new Grid { Children = { video, label, close } };
    }
}
