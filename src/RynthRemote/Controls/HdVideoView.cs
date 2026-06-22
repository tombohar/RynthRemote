namespace RynthRemote.Controls;

/// Cross-platform handle for the native HD video surface. The iOS handler maps this onto an
/// H264StreamView (AVSampleBufferDisplayLayer fed by the agent's WebSocket H.264). Other platforms
/// render nothing (HD is iOS-only for now).
public sealed class HdVideoView : View
{
    public string BaseUrl { get; set; } = "";
    public string Token { get; set; } = "";
    public int Pid { get; set; }
}
