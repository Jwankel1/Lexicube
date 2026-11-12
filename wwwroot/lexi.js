// Lexicube browser side: physical keyboard, synthesized sound, and the win celebration.
// Sounds are generated with the Web Audio API, so there are no audio files to ship.
(() => {
  let dotnet = null;
  let ctx = null;
  let noise = null;
  let muted = false;
  try { muted = localStorage.getItem('lexi.muted') === '1'; } catch { }

  const reducedMotion = () => matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Browsers only allow audio after a user gesture, so the context is created lazily.
  function audio() {
    if (muted) return null;
    try {
      ctx ??= new (window.AudioContext || window.webkitAudioContext)();
      if (ctx.state === 'suspended') ctx.resume();
      return ctx;
    } catch { return null; }
  }

  function tone({ f, at = 0, d = 0.15, type = 'sine', v = 0.12, to = null }) {
    const a = audio(); if (!a) return;
    const t = a.currentTime + at;
    const osc = a.createOscillator(), gain = a.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(f, t);
    if (to) osc.frequency.exponentialRampToValueAtTime(to, t + d);
    gain.gain.setValueAtTime(0.0001, t);
    gain.gain.exponentialRampToValueAtTime(v, t + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + d);
    osc.connect(gain).connect(a.destination);
    osc.start(t); osc.stop(t + d + 0.05);
  }

  function whoosh(at, d = 0.35, v = 0.05) {
    const a = audio(); if (!a) return;
    if (!noise) {
      noise = a.createBuffer(1, a.sampleRate, a.sampleRate);
      const data = noise.getChannelData(0);
      for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    }
    const t = a.currentTime + at;
    const src = a.createBufferSource(), filter = a.createBiquadFilter(), gain = a.createGain();
    src.buffer = noise;
    filter.type = 'bandpass'; filter.Q.value = 1.2;
    filter.frequency.setValueAtTime(300, t);
    filter.frequency.exponentialRampToValueAtTime(2400, t + d);
    gain.gain.setValueAtTime(0.0001, t);
    gain.gain.exponentialRampToValueAtTime(v, t + d * 0.5);
    gain.gain.exponentialRampToValueAtTime(0.0001, t + d);
    src.connect(filter).connect(gain).connect(a.destination);
    src.start(t); src.stop(t + d + 0.05);
  }

  const tick = () => tone({ f: 880, d: 0.04, type: 'triangle', v: 0.05 });

  function onKeyDown(e) {
    if (!dotnet || e.ctrlKey || e.metaKey || e.altKey) return;
    const tag = e.target?.tagName;
    if (tag === 'INPUT' || tag === 'TEXTAREA') return;

    let key = null;
    if (e.key === 'Enter') key = 'ENTER';
    else if (e.key === 'Backspace') key = 'BACK';
    else if (e.key === 'Escape') key = 'ESC';
    else if (/^[a-zA-Z]$/.test(e.key)) key = e.key.toUpperCase();
    if (!key) return;

    if (document.querySelector('.modal') && key !== 'ESC') return;
    e.preventDefault(); // stops Enter from also "clicking" a focused on-screen key
    if (key !== 'ESC') tick();
    dotnet.invokeMethodAsync('OnKey', key);
  }

  function onPointerDown(e) {
    if (e.target.closest?.('[data-key]')) tick();
  }

  // ---------- celebration ----------
  const COLORS = ['#19c37d', '#f5b83d', '#7c5cff', '#22d3ee', '#ff5c93', '#ffffff'];

  function celebrate() {
    if (reducedMotion()) return;
    const canvas = document.createElement('canvas');
    canvas.className = 'confetti';
    document.body.appendChild(canvas);
    const g = canvas.getContext('2d');
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const size = () => { canvas.width = innerWidth * dpr; canvas.height = innerHeight * dpr; };
    size();
    addEventListener('resize', size);

    const parts = [];
    const cannon = (x, y, angle, n) => {
      for (let i = 0; i < n; i++) {
        const a = angle + (Math.random() - 0.5) * 0.9, s = 9 + Math.random() * 13;
        parts.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, g: 0.28, drag: 0.985,
          w: 6 + Math.random() * 7, h: 3 + Math.random() * 5, rot: Math.random() * 6.3, vr: (Math.random() - 0.5) * 0.5,
          color: COLORS[(Math.random() * COLORS.length) | 0], life: 220 + Math.random() * 80, spark: false });
      }
    };
    const firework = (x, y) => {
      const color = COLORS[(Math.random() * (COLORS.length - 1)) | 0];
      for (let i = 0; i < 70; i++) {
        const a = (i / 70) * Math.PI * 2, s = 2 + Math.random() * 6;
        parts.push({ x, y, vx: Math.cos(a) * s, vy: Math.sin(a) * s, g: 0.05, drag: 0.965,
          w: 3, h: 3, rot: 0, vr: 0, color, life: 55 + Math.random() * 35, spark: true });
      }
      tone({ f: 180, d: 0.25, type: 'triangle', v: 0.06, to: 60 });
      whoosh(0, 0.5, 0.04);
    };

    const W = () => innerWidth, H = () => innerHeight;
    cannon(0, H(), -Math.PI / 3.2, 110);
    cannon(W(), H(), -Math.PI + Math.PI / 3.2, 110);
    const timers = [400, 800, 1250, 1700, 2200, 2700].map(ms =>
      setTimeout(() => firework(W() * (0.15 + Math.random() * 0.7), H() * (0.12 + Math.random() * 0.35)), ms));

    const started = performance.now();
    (function frame(now) {
      g.setTransform(dpr, 0, 0, dpr, 0, 0);
      g.clearRect(0, 0, W(), H());
      for (let i = parts.length - 1; i >= 0; i--) {
        const p = parts[i];
        p.vx *= p.drag; p.vy = p.vy * p.drag + p.g;
        p.x += p.vx; p.y += p.vy; p.rot += p.vr; p.life--;
        if (p.life <= 0 || p.y > H() + 40) { parts.splice(i, 1); continue; }
        g.globalAlpha = Math.min(1, p.life / 30);
        g.fillStyle = p.color;
        if (p.spark) {
          g.shadowColor = p.color; g.shadowBlur = 8;
          g.beginPath(); g.arc(p.x, p.y, 2, 0, 6.3); g.fill();
          g.shadowBlur = 0;
        } else {
          g.save(); g.translate(p.x, p.y); g.rotate(p.rot);
          g.scale(1, Math.cos(p.rot * 2)); // fake the paper tumbling in 3D
          g.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
          g.restore();
        }
      }
      if ((parts.length > 0 || now - started < 3000) && now - started < 9000) requestAnimationFrame(frame);
      else { timers.forEach(clearTimeout); removeEventListener('resize', size); canvas.remove(); }
    })(started);
  }

  window.lexi = {
    init(ref) {
      dotnet = ref;
      document.addEventListener('keydown', onKeyDown);
      document.addEventListener('pointerdown', onPointerDown);
    },
    dispose() {
      dotnet = null;
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('pointerdown', onPointerDown);
    },
    isMuted: () => muted,
    setMuted(value) {
      muted = !!value;
      try { localStorage.setItem('lexi.muted', muted ? '1' : '0'); } catch { }
      if (!muted) tone({ f: 660, d: 0.12, v: 0.08 });
    },
    // states: 0 absent, 1 present, 2 correct. Timings mirror the CSS flip (0.22s stagger, 0.8s spin).
    reveal(states) {
      states.forEach((s, i) => {
        const start = i * 0.22, land = start + 0.72;
        whoosh(start, 0.6, 0.035);
        if (s === 2) { tone({ f: 784, at: land, d: 0.4, v: 0.11 }); tone({ f: 1568, at: land, d: 0.3, v: 0.04 }); }
        else if (s === 1) { tone({ f: 523, at: land, d: 0.25, type: 'triangle', v: 0.1 }); }
        else { tone({ f: 150, at: land, d: 0.12, type: 'triangle', v: 0.12, to: 90 }); }
      });
    },
    buzz() {
      tone({ f: 130, d: 0.16, type: 'sawtooth', v: 0.07 });
      tone({ f: 98, at: 0.09, d: 0.2, type: 'sawtooth', v: 0.07 });
    },
    win() {
      [523, 659, 784, 1047, 1319, 1568].forEach((f, i) => tone({ f, at: i * 0.09, d: 0.5, type: 'triangle', v: 0.11 }));
      [523, 659, 784, 1047].forEach(f => tone({ f, at: 0.62, d: 1.6, v: 0.06 }));
      for (let i = 0; i < 10; i++) tone({ f: 1800 + Math.random() * 1800, at: 0.7 + i * 0.13, d: 0.18, v: 0.025 });
      celebrate();
    },
    lose() {
      [392, 349, 311, 233].forEach((f, i) => tone({ f, at: i * 0.24, d: 0.42, type: 'triangle', v: 0.1 }));
    },
    async copy(text) {
      try { await navigator.clipboard.writeText(text); return true; }
      catch {
        // Clipboard API needs HTTPS or localhost; fall back for plain-HTTP LAN hosting.
        try {
          const ta = document.createElement('textarea');
          ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0';
          document.body.appendChild(ta); ta.select();
          const ok = document.execCommand('copy');
          ta.remove();
          return ok;
        } catch { return false; }
      }
    },
  };
})();
