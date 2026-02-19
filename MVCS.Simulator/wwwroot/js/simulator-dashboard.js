// =============================================
// MVCS Simulator - Dashboard JavaScript
// Real-time hardware visualization & control
// =============================================

// ---- State ----
let currentHeading = 0;
let currentCardinal = 'N';
let ledIsOn = false;

// ---- Live Clock ----
function updateClock() {
    const el = document.getElementById('clockDisplay');
    if (el) {
        const now = new Date();
        el.textContent = now.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
            + '  ' + now.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }
}
window.setInterval(updateClock, 1000);
updateClock();

// ---- SignalR Connection (local SimulatorDashboardHub) ----
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/simulatordashboardhub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

connection.onreconnecting(() => setConnectionStatus(false));
connection.onreconnected(() => setConnectionStatus(true));
connection.onclose(() => setConnectionStatus(false));

function setConnectionStatus(connected) {
    const bar = document.getElementById('connectionBar');
    bar.className = connected
        ? 'h-1 bg-green-500 transition-all duration-500 shadow-sm shadow-green-500/50'
        : 'h-1 bg-red-500 transition-all duration-500 shadow-sm shadow-red-500/50 animate-pulse';
}

// ---- SignalR Handlers ----
connection.on("ReceiveCompass", (heading, cardinal) => {
    currentHeading = heading;
    currentCardinal = cardinal;
    document.getElementById('compassText').textContent = `${cardinal} ${heading}°`;
    drawCompass(heading);
});

connection.on("ReceiveWaterLevel", (level, status) => {
    const fill = document.getElementById('waterFill');
    const percent = document.getElementById('waterPercent');
    const statusEl = document.getElementById('waterStatus');

    fill.style.height = `${level}%`;
    percent.textContent = `${Math.round(level)}%`;

    const cfg = {
        'HIGH': { bg: 'bg-red-500/20', text: 'text-red-400' },
        'NORMAL': { bg: 'bg-green-500/20', text: 'text-green-400' },
        'LOW': { bg: 'bg-yellow-500/20', text: 'text-yellow-400' }
    };
    const c = cfg[status] || cfg['NORMAL'];
    statusEl.className = `px-2.5 py-0.5 rounded-full text-xs font-bold ${c.bg} ${c.text} transition-all`;
    statusEl.textContent = status;
});

connection.on("ReceivePumpState", (isOn, message) => {
    const toggle = document.getElementById('pumpToggle');
    toggle.checked = isOn;
    updatePumpVisual(isOn);
});

connection.on("ReceiveLedState", (hexColor, brightness) => {
    updateLedVisual(hexColor, brightness);
    // Sync the LED toggle state
    if (hexColor !== '#000000' && brightness > 0) {
        if (!ledIsOn) {
            ledIsOn = true;
            document.getElementById('ledToggle').checked = true;
            const controls = document.getElementById('ledControls');
            controls.classList.remove('opacity-30', 'pointer-events-none');
            controls.classList.add('opacity-100');
            const badge = document.getElementById('ledBadge');
            badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-500/20 text-amber-400 transition-all';
            badge.textContent = 'ON';
        }
        document.getElementById('ledColorPicker').value = hexColor;
        document.getElementById('ledBrightness').value = brightness;
        document.getElementById('brightnessVal').textContent = brightness + '%';
    }
});

connection.on("ReceiveHardwareState", (state) => {
    const badge = document.getElementById('serverBadge');
    if (state.isGlobalRunning) {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
        badge.textContent = '● Running';
    } else {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
        badge.textContent = '● Standby';
    }
    setHwBadge('compassHwBadge', 'compassOverlay', state.isCompassEnabled);
    setHwBadge('waterHwBadge', 'waterOverlay', state.isWaterEnabled);
    setHwBadge('pumpHwBadge', 'pumpOverlay', state.isPumpEnabled);
    setHwBadge('ledHwBadge', 'ledOverlay', state.isLedEnabled);

    // Sync interval inputs
    if (state.compassIntervalMs) {
        document.getElementById('compassInterval').value = state.compassIntervalMs;
    }
    if (state.waterIntervalMs) {
        document.getElementById('waterInterval').value = state.waterIntervalMs;
    }
});

// ---- Start Connection ----
async function startConnection() {
    try {
        await connection.start();
        setConnectionStatus(true);
        checkSimulatorState();
    } catch (err) {
        console.error("SignalR error:", err);
        setConnectionStatus(false);
        setTimeout(startConnection, 5000);
    }
}

// ---- Hardware State ----
function setHwBadge(badgeId, overlayId, enabled) {
    const badge = document.getElementById(badgeId);
    const overlay = document.getElementById(overlayId);

    if (badge) {
        if (enabled) {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105';
            badge.textContent = 'HW ON';
        } else {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse cursor-pointer hover:scale-105';
            badge.textContent = 'HW OFF';
        }
    }
    if (overlay) {
        if (enabled) {
            overlay.classList.add('hidden');
            overlay.classList.remove('flex');
        } else {
            overlay.classList.remove('hidden');
            overlay.classList.add('flex');
        }
    }
}

async function toggleHardware(component) {
    try {
        const res = await fetch(`/api/simulation/toggle/${component}`, { method: 'POST' });
        if (!res.ok) {
            Swal.fire({
                icon: 'error',
                title: 'Toggle Failed',
                text: 'Could not toggle hardware.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#ea580c',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
        // State update will arrive via SignalR ReceiveHardwareState
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Cannot reach the simulator API.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#ea580c'
        });
    }
}

async function checkSimulatorState() {
    try {
        const res = await fetch('/api/simulation/state');
        const data = await res.json();
        const badge = document.getElementById('serverBadge');
        if (data.isGlobalRunning) {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
            badge.textContent = '● Running';
        } else {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
            badge.textContent = '● Standby';
        }

        setHwBadge('compassHwBadge', 'compassOverlay', data.isCompassEnabled);
        setHwBadge('waterHwBadge', 'waterOverlay', data.isWaterEnabled);
        setHwBadge('pumpHwBadge', 'pumpOverlay', data.isPumpEnabled);
        setHwBadge('ledHwBadge', 'ledOverlay', data.isLedEnabled);

        // Sync interval inputs
        if (data.compassIntervalMs) {
            document.getElementById('compassInterval').value = data.compassIntervalMs;
        }
        if (data.waterIntervalMs) {
            document.getElementById('waterInterval').value = data.waterIntervalMs;
        }
    } catch {
        const badge = document.getElementById('serverBadge');
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse';
        badge.textContent = '● Error';
    }
}

// ---- Interval Controls ----
async function setIntervalMs(component, value) {
    try {
        const res = await fetch(`/api/simulation/interval/${component}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ intervalMs: parseInt(value) })
        });
        if (res.ok) {
            const data = await res.json();
            document.getElementById(component + 'Interval').value = data.intervalMs;
            Swal.fire({
                icon: 'success',
                title: 'Interval Updated',
                text: `${component} interval set to ${data.intervalMs}ms`,
                timer: 1500,
                showConfirmButton: false,
                background: '#1e293b',
                color: '#e2e8f0',
                backdrop: 'rgba(0,0,0,0.4)'
            });
        }
    } catch {
        console.error('Failed to set interval for', component);
    }
}

// Expose as global for the HTML onchange
window.setSimInterval = setIntervalMs;

// ---- Compass Drawing (Canvas) ----
function drawCompass(heading) {
    const canvas = document.getElementById('compassGauge');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const w = canvas.width, h = canvas.height;
    const cx = w / 2, cy = h / 2;
    const r = Math.min(cx, cy) - 10;

    ctx.clearRect(0, 0, w, h);

    // Outer glow ring
    const gradient = ctx.createRadialGradient(cx, cy, r - 5, cx, cy, r + 5);
    gradient.addColorStop(0, 'rgba(14,165,233,0.1)');
    gradient.addColorStop(1, 'transparent');
    ctx.beginPath();
    ctx.arc(cx, cy, r + 3, 0, Math.PI * 2);
    ctx.fillStyle = gradient;
    ctx.fill();

    // Outer circle
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Inner dial bg
    const dialGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
    dialGrad.addColorStop(0, '#1e293b');
    dialGrad.addColorStop(1, '#0f172a');
    ctx.beginPath();
    ctx.arc(cx, cy, r - 1, 0, Math.PI * 2);
    ctx.fillStyle = dialGrad;
    ctx.fill();

    // Tick marks
    for (let deg = 0; deg < 360; deg += 5) {
        const angle = (deg - heading - 90) * Math.PI / 180;
        const isMajor = deg % 30 === 0;
        const isMid = deg % 10 === 0;
        const tickLen = isMajor ? 14 : (isMid ? 8 : 4);
        const x1 = cx + (r - 3) * Math.cos(angle);
        const y1 = cy + (r - 3) * Math.sin(angle);
        const x2 = cx + (r - 3 - tickLen) * Math.cos(angle);
        const y2 = cy + (r - 3 - tickLen) * Math.sin(angle);

        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.strokeStyle = isMajor ? '#64748b' : (isMid ? '#475569' : '#334155');
        ctx.lineWidth = isMajor ? 2 : 1;
        ctx.stroke();
    }

    // Cardinal marks
    const cardinals = [
        { label: 'N', deg: 0, color: '#ef4444' },
        { label: 'NE', deg: 45, color: '#64748b' },
        { label: 'E', deg: 90, color: '#94a3b8' },
        { label: 'SE', deg: 135, color: '#64748b' },
        { label: 'S', deg: 180, color: '#94a3b8' },
        { label: 'SW', deg: 225, color: '#64748b' },
        { label: 'W', deg: 270, color: '#94a3b8' },
        { label: 'NW', deg: 315, color: '#64748b' }
    ];
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    for (const c of cardinals) {
        const angle = (c.deg - heading - 90) * Math.PI / 180;
        const dist = c.label.length === 1 ? r - 25 : r - 24;
        const tx = cx + dist * Math.cos(angle);
        const ty = cy + dist * Math.sin(angle);
        ctx.font = c.label.length === 1 ? 'bold 13px sans-serif' : '9px sans-serif';
        ctx.fillStyle = c.color;
        ctx.fillText(c.label, tx, ty);
    }

    // Degree numbers for major ticks
    ctx.font = '7px monospace';
    ctx.fillStyle = '#475569';
    for (let deg = 0; deg < 360; deg += 30) {
        if (deg % 90 === 0) continue;
        const angle = (deg - heading - 90) * Math.PI / 180;
        const tx = cx + (r - 38) * Math.cos(angle);
        const ty = cy + (r - 38) * Math.sin(angle);
        ctx.fillText(`${deg}`, tx, ty);
    }

    // North needle (red, pointed)
    ctx.save();
    ctx.translate(cx, cy);
    ctx.beginPath();
    ctx.moveTo(0, -(r - 40));
    ctx.lineTo(-5, 0);
    ctx.lineTo(0, -8);
    ctx.lineTo(5, 0);
    ctx.closePath();
    ctx.fillStyle = '#ef4444';
    ctx.shadowColor = '#ef4444';
    ctx.shadowBlur = 8;
    ctx.fill();
    ctx.shadowBlur = 0;

    // South needle
    ctx.beginPath();
    ctx.moveTo(0, r - 40);
    ctx.lineTo(-5, 0);
    ctx.lineTo(0, 8);
    ctx.lineTo(5, 0);
    ctx.closePath();
    ctx.fillStyle = '#334155';
    ctx.fill();
    ctx.restore();

    // Center hub
    ctx.beginPath();
    ctx.arc(cx, cy, 6, 0, Math.PI * 2);
    ctx.fillStyle = '#e2e8f0';
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx, cy, 3, 0, Math.PI * 2);
    ctx.fillStyle = '#94a3b8';
    ctx.fill();
}

// ---- Pump Control ----
function updatePumpVisual(isOn) {
    const icon = document.getElementById('pumpIcon');
    const status = document.getElementById('pumpStatus');
    const badge = document.getElementById('pumpBadge');

    if (isOn) {
        icon.classList.add('pump-spin');
        icon.className = icon.className.replace('bg-slate-700', 'bg-emerald-900/50');
        status.textContent = 'Running';
        status.className = 'text-emerald-400 text-sm font-semibold';
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-500/20 text-emerald-400 transition-all';
        badge.textContent = 'ON';
    } else {
        icon.classList.remove('pump-spin');
        icon.className = 'w-16 h-16 rounded-full bg-slate-700 flex items-center justify-center text-3xl transition-all duration-300';
        status.textContent = 'Idle';
        status.className = 'text-slate-500 text-sm font-medium';
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
        badge.textContent = 'OFF';
    }
}

async function togglePump() {
    const toggle = document.getElementById('pumpToggle');
    const wantOn = toggle.checked;

    try {
        const res = await fetch('/api/hardware/pump', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isOn: wantOn, message: wantOn ? 'Manual ON' : 'Manual OFF' })
        });

        if (res.ok) {
            updatePumpVisual(wantOn);
        } else {
            toggle.checked = !wantOn;
            Swal.fire({
                icon: 'error',
                title: 'Pump Control Failed',
                text: 'Hardware disabled.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#ea580c',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
    } catch {
        toggle.checked = !wantOn;
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Cannot reach the simulator API.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#ea580c'
        });
    }
}

// ---- LED Control ----
function toggleLed() {
    const toggle = document.getElementById('ledToggle');
    ledIsOn = toggle.checked;

    const controls = document.getElementById('ledControls');
    const badge = document.getElementById('ledBadge');
    const dot = document.getElementById('ledDot');

    if (ledIsOn) {
        controls.classList.remove('opacity-30', 'pointer-events-none');
        controls.classList.add('opacity-100');
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-500/20 text-amber-400 transition-all';
        badge.textContent = 'ON';
        const color = document.getElementById('ledColorPicker').value;
        setLedColor(color);
    } else {
        controls.classList.add('opacity-30', 'pointer-events-none');
        controls.classList.remove('opacity-100');
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
        badge.textContent = 'OFF';
        dot.className = 'w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300';
        setLedColor('#000000', true);
    }
}

async function setLedColor(hexColor, forceOff) {
    const brightness = forceOff ? 0 : parseInt(document.getElementById('ledBrightness').value);

    try {
        const res = await fetch('/api/hardware/led', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ hexColor: hexColor, brightness: brightness })
        });

        if (res.ok) {
            if (forceOff) {
                updateLedVisual('#000000', 0);
            } else {
                updateLedVisual(hexColor, brightness);
            }
        } else {
            if (ledIsOn) {
                document.getElementById('ledToggle').checked = false;
                ledIsOn = false;
                toggleLed();
            }
            Swal.fire({
                icon: 'error',
                title: 'LED Control Failed',
                text: 'Hardware disabled.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#ea580c'
            });
        }
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Error',
            text: 'Cannot reach the simulator API.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#ea580c'
        });
    }
}

function updateLedVisual(hexColor, brightness) {
    const orb = document.getElementById('ledOrb');
    const hexText = document.getElementById('ledHexText');
    const dot = document.getElementById('ledDot');
    const opacity = brightness / 100;
    const isOff = hexColor === '#000000' || brightness === 0;

    if (isOff) {
        orb.style.background = '#334155';
        orb.style.borderColor = '#475569';
        orb.style.boxShadow = 'none';
        dot.className = 'w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300';
        hexText.textContent = '#000000';
    } else {
        orb.style.background = `radial-gradient(circle at 40% 40%, ${hexColor}dd, ${hexColor}66)`;
        orb.style.borderColor = hexColor;
        orb.style.boxShadow = `0 0 ${20 * opacity}px ${hexColor}, 0 0 ${50 * opacity}px ${hexColor}44, inset 0 0 ${15 * opacity}px ${hexColor}66`;
        dot.style.backgroundColor = hexColor;
        dot.style.boxShadow = `0 0 6px ${hexColor}`;
        dot.className = 'w-2 h-2 rounded-full inline-block transition-all duration-300';
        hexText.textContent = hexColor.toUpperCase();
    }
}

// ---- Init ----
document.addEventListener('DOMContentLoaded', () => {
    drawCompass(0);
    startConnection();
});
