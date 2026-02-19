// =============================================
// MVCS Pro - Dashboard JavaScript v2
// Enhanced UI: Animations, LED Toggle, Better Compass
// =============================================

// ---- State ----
let currentHeading = 0;
let currentCardinal = 'N';
let waterChart = null;
let ledIsOn = false;
let lastSimulatorUpdate = Date.now();

// ---- Live Clock ----
function updateClock() {
    const el = document.getElementById('clockDisplay');
    if (el) {
        const now = new Date();
        el.textContent = now.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
            + '  ' + now.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }
}
setInterval(updateClock, 1000);
updateClock();

// ---- SignalR Connection ----
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/vesselhub")
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
connection.on("ConnectionStatus", (status) => setConnectionStatus(status));

connection.on("ReceiveCompass", (heading, cardinal) => {
    lastSimulatorUpdate = Date.now();
    currentHeading = heading;
    currentCardinal = cardinal;
    document.getElementById('compassText').textContent = `${cardinal} ${heading}°`;
    drawCompass(heading);
});

connection.on("ReceiveWaterLevel", (level, status) => {
    lastSimulatorUpdate = Date.now();
    const fill = document.getElementById('waterFill');
    const percent = document.getElementById('waterPercent');
    const statusEl = document.getElementById('waterStatus');

    fill.style.height = `${level}%`;
    percent.textContent = `${Math.round(level)}%`;

    const cfg = {
        'HIGH': { bg: 'bg-red-500/20', text: 'text-red-400', border: '' },
        'NORMAL': { bg: 'bg-green-500/20', text: 'text-green-400', border: '' },
        'LOW': { bg: 'bg-yellow-500/20', text: 'text-yellow-400', border: '' }
    };
    const c = cfg[status] || cfg['NORMAL'];
    statusEl.className = `px-2.5 py-0.5 rounded-full text-xs font-bold ${c.bg} ${c.text} transition-all`;
    statusEl.textContent = status;
});

connection.on("ReceivePumpState", (isOn, message) => {
    lastSimulatorUpdate = Date.now();
    const toggle = document.getElementById('pumpToggle');
    toggle.checked = isOn;
    updatePumpVisual(isOn);
});

connection.on("ReceiveLedState", (hexColor, brightness) => {
    lastSimulatorUpdate = Date.now();
    const isOn = hexColor !== '#000000' && brightness > 0;
    if (isOn) {
        // Sync toggle and controls to ON
        if (!ledIsOn) {
            ledIsOn = true;
            const toggle = document.getElementById('ledToggle');
            if (toggle) toggle.checked = true;
            const controls = document.getElementById('ledControls');
            if (controls) {
                controls.classList.remove('opacity-30', 'pointer-events-none');
                controls.classList.add('opacity-100');
            }
            const badge = document.getElementById('ledBadge');
            if (badge) {
                badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-500/20 text-amber-400 transition-all';
                badge.textContent = 'ON';
            }
        }
        const picker = document.getElementById('ledColorPicker');
        if (picker) picker.value = hexColor;
        const slider = document.getElementById('ledBrightness');
        if (slider) slider.value = brightness;
        const bVal = document.getElementById('brightnessVal');
        if (bVal) bVal.textContent = brightness + '%';
        updateLedVisual(hexColor, brightness);
    } else {
        // Sync toggle and controls to OFF
        ledIsOn = false;
        const toggle = document.getElementById('ledToggle');
        if (toggle) toggle.checked = false;
        const controls = document.getElementById('ledControls');
        if (controls) {
            controls.classList.add('opacity-30', 'pointer-events-none');
            controls.classList.remove('opacity-100');
        }
        const badge = document.getElementById('ledBadge');
        if (badge) {
            badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
            badge.textContent = 'OFF';
        }
        updateLedVisual('#000000', 0);
    }
});

connection.on("ReceiveHardwareState", (state) => {
    lastSimulatorUpdate = Date.now();
    const badge = document.getElementById('simulatorBadge');
    if (state.isGlobalRunning) {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
        badge.textContent = '● Running';
    } else {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
        badge.textContent = '● Standby';
    }
    simulatorOnline = true;
    setHwBadge('compassHwBadge', 'compassOverlay', state.isCompassEnabled, false);
    setHwBadge('waterHwBadge', 'waterOverlay', state.isWaterEnabled, false);
    setHwBadge('pumpHwBadge', 'pumpOverlay', state.isPumpEnabled, false);
    setHwBadge('ledHwBadge', 'ledOverlay', state.isLedEnabled, false);
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

// ---- Simulator State ----
let simulatorOnline = true;

function setHwBadge(badgeId, overlayId, enabled, offline) {
    const badge = document.getElementById(badgeId);
    const overlay = document.getElementById(overlayId);
    const component = overlayId.replace('Overlay', '');
    const overlayText = document.getElementById(component + 'OverlayText');
    const overlayBtn = document.getElementById(component + 'OverlayBtn');

    if (badge) {
        if (offline) {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-slate-500/15 text-slate-400 border border-slate-500/30 transition-all duration-300 cursor-not-allowed';
            badge.textContent = 'OFFLINE';
        } else if (enabled) {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105';
            badge.textContent = 'HW ON';
        } else {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse cursor-pointer hover:scale-105';
            badge.textContent = 'HW OFF';
        }
    }
    if (overlay) {
        if (enabled && !offline) {
            overlay.classList.add('hidden');
            overlay.classList.remove('flex');
        } else {
            overlay.classList.remove('hidden');
            overlay.classList.add('flex');
        }
    }
    if (overlayText) {
        overlayText.textContent = offline ? 'SIMULATOR OFFLINE' : 'HARDWARE DISABLED';
        overlayText.className = offline
            ? 'text-slate-400 font-bold text-sm'
            : 'text-red-400 font-bold text-sm';
    }
    if (overlayBtn) {
        overlayBtn.style.display = offline ? 'none' : '';
    }
}

async function toggleHardware(component) {
    if (!simulatorOnline) {
        Swal.fire({
            icon: 'warning',
            title: 'Simulator Offline',
            text: 'Cannot toggle hardware while simulator is offline.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb',
            backdrop: 'rgba(0,0,0,0.6)'
        });
        return;
    }
    try {
        const res = await fetch(`/api/vessel/toggle/${component}`, { method: 'POST' });
        if (!res.ok) {
            Swal.fire({
                icon: 'error',
                title: 'Toggle Failed',
                text: 'Could not toggle hardware. Simulator may be offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
        // State update will arrive via SignalR ReceiveHardwareState
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
        });
    }
}

async function checkSimulatorState() {
    try {
        const res = await fetch('/api/vessel/simulator/state');
        const data = await res.json();
        const badge = document.getElementById('simulatorBadge');
        if (data.isGlobalRunning) {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
            badge.textContent = '● Running';
        } else {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
            badge.textContent = '● Standby';
        }

        // Per-hardware status
        simulatorOnline = true;
        setHwBadge('compassHwBadge', 'compassOverlay', data.isCompassEnabled, false);
        setHwBadge('waterHwBadge', 'waterOverlay', data.isWaterEnabled, false);
        setHwBadge('pumpHwBadge', 'pumpOverlay', data.isPumpEnabled, false);
        setHwBadge('ledHwBadge', 'ledOverlay', data.isLedEnabled, false);
    } catch {
        simulatorOnline = false;
        const badge = document.getElementById('simulatorBadge');
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse';
        badge.textContent = '● Offline';
        // Mark all hardware as offline
        setHwBadge('compassHwBadge', 'compassOverlay', false, true);
        setHwBadge('waterHwBadge', 'waterOverlay', false, true);
        setHwBadge('pumpHwBadge', 'pumpOverlay', false, true);
        setHwBadge('ledHwBadge', 'ledOverlay', false, true);
    }
}
setInterval(checkSimulatorState, 10000);

// ---- Simulator Heartbeat (5s timeout) ----
setInterval(() => {
    const elapsed = Date.now() - lastSimulatorUpdate;
    if (elapsed > 5000 && simulatorOnline) {
        simulatorOnline = false;
        const badge = document.getElementById('simulatorBadge');
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse';
        badge.textContent = '● Offline';
        setHwBadge('compassHwBadge', 'compassOverlay', false, true);
        setHwBadge('waterHwBadge', 'waterOverlay', false, true);
        setHwBadge('pumpHwBadge', 'pumpOverlay', false, true);
        setHwBadge('ledHwBadge', 'ledOverlay', false, true);
    }
}, 1000);

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
        ctx.font = c.label.length === 1 ? 'bold 14px sans-serif' : '10px sans-serif';
        ctx.fillStyle = c.color;
        ctx.fillText(c.label, tx, ty);
    }

    // Degree numbers for major ticks
    ctx.font = '8px monospace';
    ctx.fillStyle = '#475569';
    for (let deg = 0; deg < 360; deg += 30) {
        if (deg % 90 === 0) continue; // skip cardinals
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
        const res = await fetch('/api/vessel/pump', {
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
                text: 'Hardware disabled or simulator offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
    } catch {
        toggle.checked = !wantOn;
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
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
        // Send current color
        const color = document.getElementById('ledColorPicker').value;
        setLedColor(color);
    } else {
        controls.classList.add('opacity-30', 'pointer-events-none');
        controls.classList.remove('opacity-100');
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
        badge.textContent = 'OFF';
        dot.className = 'w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300';
        // Turn off: send black / 0 brightness
        setLedColor('#000000', true);
    }
}

async function setLedColor(hexColor, forceOff) {
    const brightness = forceOff ? 0 : parseInt(document.getElementById('ledBrightness').value);
    if (!forceOff) {
        document.getElementById('brightnessVal').textContent = `${brightness}%`;
    }

    try {
        const res = await fetch('/api/vessel/led', {
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
                text: 'Hardware disabled or simulator offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb'
            });
        }
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
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

// ---- Modals ----
function openModal(id) {
    const modal = document.getElementById(id);
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    // Animate in
    const panel = modal.querySelector('.relative');
    if (panel) {
        panel.style.transform = 'scale(0.95)';
        panel.style.opacity = '0';
        requestAnimationFrame(() => {
            panel.style.transition = 'transform 0.2s ease, opacity 0.2s ease';
            panel.style.transform = 'scale(1)';
            panel.style.opacity = '1';
        });
    }
}

function closeModal(id) {
    const modal = document.getElementById(id);
    const panel = modal.querySelector('.relative');
    if (panel) {
        panel.style.transform = 'scale(0.95)';
        panel.style.opacity = '0';
        setTimeout(() => {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
        }, 200);
    } else {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

// ---- Water History Modal ----
async function openWaterHistory() {
    openModal('waterHistoryModal');

    try {
        const res = await fetch('/api/vessel/history/water');
        const data = await res.json();

        const labels = data.map(d => new Date(d.timestamp).toLocaleTimeString());
        const levels = data.map(d => d.level);

        if (waterChart) waterChart.destroy();

        const ctx = document.getElementById('waterChart').getContext('2d');
        const gradientFill = ctx.createLinearGradient(0, 0, 0, 300);
        gradientFill.addColorStop(0, 'rgba(59,130,246,0.3)');
        gradientFill.addColorStop(1, 'rgba(59,130,246,0.02)');

        waterChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Water Level (%)',
                    data: levels,
                    borderColor: '#3b82f6',
                    backgroundColor: gradientFill,
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0,
                    pointHoverRadius: 5,
                    pointHoverBackgroundColor: '#3b82f6',
                    pointHoverBorderColor: '#fff',
                    pointHoverBorderWidth: 2
                }]
            },
            options: {
                responsive: true,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { color: '#94a3b8', usePointStyle: true, padding: 20 } },
                    tooltip: {
                        backgroundColor: '#1e293b',
                        titleColor: '#94a3b8',
                        bodyColor: '#e2e8f0',
                        borderColor: '#334155',
                        borderWidth: 1,
                        cornerRadius: 8,
                        padding: 12
                    }
                },
                scales: {
                    x: {
                        ticks: { color: '#475569', maxTicksLimit: 8, font: { size: 11 } },
                        grid: { color: '#1e293b44' }
                    },
                    y: {
                        min: 0, max: 100,
                        ticks: { color: '#475569', font: { size: 11 } },
                        grid: { color: '#1e293b44' }
                    }
                }
            }
        });
    } catch {
        console.error('Failed to load water history');
    }
}

// ---- Pump Log Modal ----
async function openPumpLogs() {
    openModal('pumpLogModal');

    try {
        const res = await fetch('/api/vessel/history/pump');
        const data = await res.json();

        const tbody = document.getElementById('pumpLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="3" class="py-8 text-center text-slate-500">No pump activity recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const stateClass = log.isOn ? 'text-emerald-400' : 'text-rose-400';
            const stateIcon = log.isOn ? '▲' : '▼';
            const stateText = log.isOn ? 'ON' : 'OFF';
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5 pr-4"><span class="${stateClass} font-bold">${stateIcon} ${stateText}</span></td>
                    <td class="py-2.5 text-slate-300">${log.message}</td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load pump logs');
    }
}

// ---- Compass Log Modal ----
async function openCompassLogs() {
    openModal('compassLogModal');

    try {
        const res = await fetch('/api/vessel/history/compass');
        const data = await res.json();

        const tbody = document.getElementById('compassLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="3" class="py-8 text-center text-slate-500">No compass data recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5 pr-4"><span class="text-cyan-400 font-bold font-mono">${log.heading}°</span></td>
                    <td class="py-2.5"><span class="px-2 py-0.5 rounded bg-cyan-500/15 text-cyan-400 text-xs font-bold">${log.cardinal}</span></td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load compass logs');
    }
}

// ---- LED Log Modal ----
async function openLedLogs() {
    openModal('ledLogModal');

    try {
        const res = await fetch('/api/vessel/history/led');
        const data = await res.json();

        const tbody = document.getElementById('ledLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="2" class="py-8 text-center text-slate-500">No LED data recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            const color = log.hexColor || '#000000';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5">
                        <div class="flex items-center gap-2">
                            <span class="w-5 h-5 rounded-full border border-slate-600 inline-block" style="background:${color}; box-shadow: 0 0 8px ${color}55;"></span>
                            <span class="font-mono text-amber-400 font-bold">${color.toUpperCase()}</span>
                        </div>
                    </td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load LED logs');
    }
}

// ---- Init ----
document.addEventListener('DOMContentLoaded', () => {
    drawCompass(0);
    startConnection();
});
