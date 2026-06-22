// HD video mode for RynthRemote. One RTCPeerConnection per streamed pid, talking to the StatusAgent's
// /webrtc/offer (browser is the offerer). recvonly => no getUserMedia, so no camera/mic permission. The
// agent answers with an H.264 SendOnly track (NVENC-class encode, proven on iOS Safari/WKWebView).
window.rynthHd = {
  conns: {},

  // Start (or restart) the HD stream for a pid, rendering into <video id=videoId>.
  async start(videoId, baseUrl, token, pid) {
    try {
      this.stop(pid);                                  // drop any existing connection for this pid first
      const pc = new RTCPeerConnection();              // no ICE servers => host candidate over Tailscale
      this.conns[pid] = pc;
      pc.addTransceiver('video', { direction: 'recvonly' });
      pc.ontrack = (e) => {
        const v = document.getElementById(videoId);
        if (v) { v.srcObject = e.streams[0]; v.play().catch(() => {}); }
      };

      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      // non-trickle: wait for host candidates to be folded into the SDP
      await new Promise((res) => {
        if (pc.iceGatheringState === 'complete') return res();
        const check = () => { if (pc.iceGatheringState === 'complete') { pc.removeEventListener('icegatheringstatechange', check); res(); } };
        pc.addEventListener('icegatheringstatechange', check);
        setTimeout(res, 1200);
      });

      let url = baseUrl.replace(/\/+$/, '') + '/webrtc/offer?pid=' + pid;
      if (token) url += '&token=' + encodeURIComponent(token);
      const resp = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sdp: pc.localDescription.sdp }),
      });
      if (!resp.ok) { this.stop(pid); return false; }
      if (this.conns[pid] !== pc) return false;        // superseded by a newer start()
      const ans = await resp.json();
      await pc.setRemoteDescription({ type: 'answer', sdp: ans.sdp });
      return true;
    } catch (e) {
      this.stop(pid);
      return false;
    }
  },

  // Stop the local peer and tell the agent to tear down the encoder (best-effort).
  stop(pid, baseUrl, token) {
    const pc = this.conns[pid];
    if (pc) { try { pc.close(); } catch (e) {} delete this.conns[pid]; }
    if (baseUrl) {
      let url = baseUrl.replace(/\/+$/, '') + '/webrtc/stop?pid=' + pid;
      if (token) url += '&token=' + encodeURIComponent(token);
      try { fetch(url, { method: 'POST' }).catch(() => {}); } catch (e) {}
    }
  },
};
