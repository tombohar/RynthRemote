namespace RynthRemote.Services;

/// Drives the native HD video overlay (iOS): show/hide + position it to a Blazor slot's rect (reported
/// from JS each frame) so the video sits inline in the card and tracks scrolling. Tap-to-expand is handled
/// natively. No-op on non-iOS platforms (HD is iOS-only for now).
public interface IHdVideoController
{
    void Show(string baseUrl, int pid, string token);
    void SetFrame(double x, double y, double w, double h);
    void Hide();
}

public sealed class HdVideoController : IHdVideoController
{
    public void Show(string baseUrl, int pid, string token) => OnMain(() =>
    {
#if IOS
        RynthRemote.Platforms.iOS.HdOverlayManager.Show(baseUrl, pid, token);
#endif
    });

    public void SetFrame(double x, double y, double w, double h) => OnMain(() =>
    {
#if IOS
        RynthRemote.Platforms.iOS.HdOverlayManager.SetFrame(x, y, w, h);
#endif
    });

    public void Hide() => OnMain(() =>
    {
#if IOS
        RynthRemote.Platforms.iOS.HdOverlayManager.Hide();
#endif
    });

    private static void OnMain(Action a)
    {
        if (MainThread.IsMainThread) a();
        else MainThread.BeginInvokeOnMainThread(a);
    }
}
