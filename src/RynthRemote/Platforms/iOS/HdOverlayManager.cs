using CoreGraphics;
using Foundation;
using UIKit;
using WebKit;

namespace RynthRemote.Platforms.iOS;

/// <summary>
/// Hosts the native H264StreamView as an overlay INSIDE the BlazorWebView, pinned to a Blazor &lt;div&gt;
/// slot whose viewport rect the JS side reports each frame (so the video tracks the card as the
/// dashboard scrolls). Tapping toggles a full-screen "expanded" frame. One overlay at a time.
/// </summary>
public static class HdOverlayManager
{
    private static H264StreamView? _view;
    private static WKWebView? _host;
    private static CGRect _inline;
    private static bool _expanded;

    public static void Show(string baseUrl, int pid, string token)
    {
        Hide();
        var web = FindWebView();
        if (web is null) return;
        _host = web;
        _view = new H264StreamView { UserInteractionEnabled = true };
        _view.Layer.ZPosition = 1000;
        _view.AddGestureRecognizer(new UITapGestureRecognizer(ToggleExpand));
        web.AddSubview(_view);
        _view.Start(baseUrl, pid, token);
    }

    /// Position the inline overlay to the slot's viewport rect (CSS px ≈ WebView points). No-op while expanded.
    public static void SetFrame(double x, double y, double w, double h)
    {
        _inline = new CGRect(x, y, w, h);
        if (_view is not null && !_expanded) _view.Frame = _inline;
    }

    public static void Hide()
    {
        _view?.Stop();
        _view?.RemoveFromSuperview();
        _view?.Dispose();
        _view = null; _host = null; _expanded = false;
    }

    private static void ToggleExpand()
    {
        if (_view is null || _host is null) return;
        _expanded = !_expanded;
        _view.Frame = _expanded ? _host.Bounds : _inline;
        if (_expanded) _host.BringSubviewToFront(_view);
    }

    private static WKWebView? FindWebView()
    {
        foreach (var w in UIApplication.SharedApplication.Windows)
        {
            var found = FindWebView(w);
            if (found is not null) return found;
        }
        return null;
    }

    private static WKWebView? FindWebView(UIView v)
    {
        if (v is WKWebView wk) return wk;
        foreach (var sub in v.Subviews)
        {
            var f = FindWebView(sub);
            if (f is not null) return f;
        }
        return null;
    }
}
