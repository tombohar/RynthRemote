using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using RynthRemote.AcStatus;
using RynthRemote.Services;

namespace RynthRemote;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // On foreground, nudge the live-view page to reconnect any streams iOS killed in the background.
        builder.ConfigureLifecycleEvents(events =>
        {
#if IOS
            events.AddiOS(ios => ios
                .OnActivated(_ => StreamLifecycle.RaiseResumed())
                .OnResignActivation(_ => StreamLifecycle.RaiseSuspended()));
#endif
        });

        builder.Services.AddMauiBlazorWebView();

        // HD video = a native AVSampleBufferDisplayLayer overlay (iOS) fed by the agent's WebSocket H.264,
        // pinned inline to the card's slot (tracked from JS) with tap-to-expand.
        builder.Services.AddSingleton<IHdVideoController, HdVideoController>();

        // Settings persisted via MAUI Preferences (the status URL + token). No database.
        builder.Services.AddSingleton<SettingsStore>();

        // The one HTTP client: talks to the user's RynthCore StatusAgent (GET /status, POST /command).
        builder.Services.AddHttpClient<IAcStatusClient, AcStatusClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
