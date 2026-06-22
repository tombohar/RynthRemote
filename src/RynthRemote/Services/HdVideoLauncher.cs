using RynthRemote.Controls;

namespace RynthRemote.Services;

/// Lets the Blazor UI open the full-screen native HD video page (pushed over the BlazorWebView host page).
public interface IHdVideoLauncher
{
    Task LaunchAsync(string baseUrl, int pid, string token, string title);
}

public sealed class HdVideoLauncher : IHdVideoLauncher
{
    public Task LaunchAsync(string baseUrl, int pid, string token, string title)
    {
        var tcs = new TaskCompletionSource();
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var page = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0].Page : null;
                if (page?.Navigation != null)
                    await page.Navigation.PushModalAsync(new HdVideoPage(baseUrl, pid, token, title));
            }
            catch { }
            finally { tcs.TrySetResult(); }
        });
        return tcs.Task;
    }
}
