using Microsoft.Maui.Handlers;
using RynthRemote.Controls;

namespace RynthRemote.Platforms.iOS;

/// Maps the cross-platform HdVideoView onto the native H264StreamView and starts streaming on create.
public sealed class HdVideoViewHandler : ViewHandler<HdVideoView, H264StreamView>
{
    public HdVideoViewHandler() : base(ViewMapper) { }

    public static readonly IPropertyMapper<HdVideoView, HdVideoViewHandler> ViewMapper =
        new PropertyMapper<HdVideoView, HdVideoViewHandler>(ViewHandler.ViewMapper);

    protected override H264StreamView CreatePlatformView() => new();

    protected override void ConnectHandler(H264StreamView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Start(VirtualView.BaseUrl, VirtualView.Pid, VirtualView.Token);
    }

    protected override void DisconnectHandler(H264StreamView platformView)
    {
        platformView.Stop();
        base.DisconnectHandler(platformView);
    }
}
