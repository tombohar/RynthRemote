// HD video mode for RynthRemote. recvonly => no getUserMedia. Signalling is done by C# (native HTTP);
// the WebView's own http fetch is blocked by WKWebView as active mixed content. This build is
// INSTRUMENTED: every step + every pc/ice state + any error is pushed to a log buffer that C# drains
// and forwards to the agent log (drainLog), so WKWebView's WebRTC errors are visible PC-side.
window.rynthHd = {
  conns: {},
  _log: [],
  _push(m) { try { this._log.push(m); if (this._log.length > 200) this._log.shift(); } catch (e) {} },
  drainLog() { const l = this._log; this._log = []; return l; },

  async createOffer(videoId, pid) {
    try {
      this.stop(pid);
      if (typeof RTCPeerConnection === 'undefined') { this._push('FATAL: RTCPeerConnection undefined (WebRTC not available in this WebView)'); return null; }
      const pc = new RTCPeerConnection();
      this.conns[pid] = { pc, videoId };
      pc.addTransceiver('video', { direction: 'recvonly' });
      pc.ontrack = (e) => { this._push('ontrack'); const v = document.getElementById(videoId); if (v) { v.srcObject = e.streams[0]; v.play().catch(err => this._push('play() rejected: ' + err)); } };
      pc.onconnectionstatechange = () => this._push('pc=' + pc.connectionState);
      pc.oniceconnectionstatechange = () => this._push('ice=' + pc.iceConnectionState);
      pc.onicegatheringstatechange = () => this._push('gather=' + pc.iceGatheringState);
      pc.onicecandidateerror = (e) => this._push('icecanderr: ' + (e.errorText || e.errorCode || ''));
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      await new Promise((res) => {
        if (pc.iceGatheringState === 'complete') return res();
        const check = () => { if (pc.iceGatheringState === 'complete') { pc.removeEventListener('icegatheringstatechange', check); res(); } };
        pc.addEventListener('icegatheringstatechange', check);
        setTimeout(res, 1200);
      });
      this._push('offer created (ice=' + pc.iceGatheringState + ')');
      return pc.localDescription ? pc.localDescription.sdp : null;
    } catch (e) {
      this._push('createOffer ERR: ' + (e.name || '') + ': ' + (e.message || e));
      this.stop(pid);
      return null;
    }
  },

  async applyAnswer(pid, answerSdp) {
    const c = this.conns[pid];
    if (!c) { this._push('applyAnswer: no peer for pid ' + pid); return 'no-peer'; }
    if (!answerSdp) { this._push('applyAnswer: empty answer'); return 'empty'; }
    try {
      await c.pc.setRemoteDescription({ type: 'answer', sdp: answerSdp });
      this._push('setRemoteDescription OK');
      return 'ok';
    } catch (e) {
      this._push('setRemoteDescription ERR: ' + (e.name || '') + ': ' + (e.message || e));
      return 'err';
    }
  },

  stop(pid) {
    const c = this.conns[pid];
    if (c) { try { c.pc.close(); } catch (e) {} delete this.conns[pid]; }
  },

  // ── Inline HD overlay tracking ──────────────────────────────────────────────────────────────
  // Report the HD slot <div>'s viewport rect to C# each animation frame (only when it changes) so the
  // native AVSampleBufferDisplayLayer overlay stays pinned to the card as the dashboard scrolls.
  _trackRaf: 0, _trackRef: null, _trackLast: "",
  startTrack(dotnetRef, slotId) {
    this.stopTrack();
    this._trackRef = dotnetRef; this._trackLast = "";
    const self = this;
    const loop = () => {
      if (!self._trackRef) return;
      const el = document.getElementById(slotId);
      if (el) {
        const r = el.getBoundingClientRect();
        const cur = `${Math.round(r.left)},${Math.round(r.top)},${Math.round(r.width)},${Math.round(r.height)}`;
        if (cur !== self._trackLast) {
          self._trackLast = cur;
          try { self._trackRef.invokeMethodAsync('OnHdRect', r.left, r.top, r.width, r.height); } catch (e) {}
        }
      }
      self._trackRaf = requestAnimationFrame(loop);
    };
    self._trackRaf = requestAnimationFrame(loop);
  },
  stopTrack() {
    if (this._trackRaf) cancelAnimationFrame(this._trackRaf);
    this._trackRaf = 0; this._trackRef = null;
  },
};

// Chat-log scroll helper. nearBottom is read from live DOM metrics (not stale C# state) so auto-follow
// only kicks in when the reader is already at the newest line; a reader scrolled up into history is left alone.
window.rynthScroll = {
  toBottom(id) { const el = document.getElementById(id); if (el) el.scrollTop = el.scrollHeight; },
  nearBottom(id, slackPx) {
    const el = document.getElementById(id); if (!el) return true;
    return (el.scrollHeight - el.scrollTop - el.clientHeight) <= (slackPx || 48);
  }
};
