const ui = {
  statusText: document.querySelector('#status .text'),
  statusDot: document.querySelector('#status .dot'),
  testSwitchButton: document.getElementById('btnTestSwitch'),
  originalModeCard: document.getElementById('originalModeCard'),
  targetMode: document.getElementById('targetMode'),
  appVersion: document.getElementById('appVersion'),
  firstRun: document.getElementById('firstRun'),
  settingsModal: document.getElementById('settingsModal'),
  bgImage: document.getElementById('bgImage'),
  bgImageInput: document.getElementById('bgImageInput'),
  startBat: document.getElementById('startBat'),
  displaySelect: document.getElementById('displaySelect'),
  startBatSetting: document.getElementById('startBatSetting'),
  displaySelectSetting: document.getElementById('displaySelectSetting'),
  originalModeInputSetting: document.getElementById('originalModeInputSetting'),
  smartDisplayToggle: document.getElementById('smartDisplayToggle'),
  themeColor: document.getElementById('themeColor'),
  themeColorText: document.getElementById('themeColorText'),
  startBatHover: document.getElementById('startBatHover'),
  primaryDisplayHover: document.getElementById('primaryDisplayHover'),
  originalModeHover: document.getElementById('originalModeHover'),
};

let currentThemeColor = '#fdd500';
let currentBgImage = localStorage.getItem('bgImage') || '';
let isTestSwitchActive = false;
let testSwitchCountdownTimer = null;
let testSwitchRemainingSeconds = 0;

const post = (type, payload = {}) => {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage({ type, payload });
  } else {
    console.log('postMessage', type, payload);
  }
};

const byId = (id) => document.getElementById(id);
const onClick = (id, handler) => {
  const el = byId(id);
  if (el) {
    el.addEventListener('click', handler);
  }
};

const setStatus = (text, color = '#7dffa0') => {
  ui.statusText.textContent = text;
  ui.statusDot.style.background = color;
  ui.statusDot.style.boxShadow = `0 0 10px ${color}`;
};

const isValidPrimaryDisplay = (value) => {
  const displayId = (value || '').trim();
  return !!displayId && displayId !== '请先选择';
};

const clamp = (v, min, max) => Math.max(min, Math.min(max, v));

const hexToRgb = (hex) => {
  const clean = hex.replace('#', '').trim();
  if (clean.length !== 6) return null;
  const r = parseInt(clean.slice(0, 2), 16);
  const g = parseInt(clean.slice(2, 4), 16);
  const b = parseInt(clean.slice(4, 6), 16);
  if (Number.isNaN(r) || Number.isNaN(g) || Number.isNaN(b)) return null;
  return { r, g, b };
};

const rgbToHex = (r, g, b) => {
  const toHex = (v) => v.toString(16).padStart(2, '0');
  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
};

const mixWithWhite = (hex, amount = 0.35) => {
  const rgb = hexToRgb(hex);
  if (!rgb) return hex;
  const r = Math.round(rgb.r + (255 - rgb.r) * amount);
  const g = Math.round(rgb.g + (255 - rgb.g) * amount);
  const b = Math.round(rgb.b + (255 - rgb.b) * amount);
  return rgbToHex(clamp(r, 0, 255), clamp(g, 0, 255), clamp(b, 0, 255));
};

const withAlpha = (hex, alpha) => {
  const rgb = hexToRgb(hex);
  if (!rgb) return `rgba(253, 213, 0, ${alpha})`;
  return `rgba(${rgb.r}, ${rgb.g}, ${rgb.b}, ${alpha})`;
};

const toCssUrl = (value) => {
  const input = (value || '').trim();
  if (!input) return '';
  if (/^https?:\/\//i.test(input) || /^data:/i.test(input) || /^file:\/\//i.test(input)) {
    return input;
  }

  const normalized = input.replace(/\\/g, '/');
  if (/^[a-zA-Z]:\//.test(normalized)) {
    return `file:///${normalized}`;
  }

  return normalized;
};

const applyThemeColor = (color) => {
  const hex = color?.toLowerCase() || '#fdd500';
  if (!/^#[0-9a-f]{6}$/.test(hex)) return;
  currentThemeColor = hex;

  const accent2 = mixWithWhite(hex, 0.35);
  document.documentElement.style.setProperty('--accent', hex);
  document.documentElement.style.setProperty('--accent-2', accent2);
  document.documentElement.style.setProperty('--logo-from', hex);
  document.documentElement.style.setProperty('--logo-to', accent2);
  document.documentElement.style.setProperty('--glow-1', withAlpha(hex, 0.45));
  document.documentElement.style.setProperty('--glow-2', withAlpha(accent2, 0.35));

  if (ui.themeColor) ui.themeColor.value = hex;
  if (ui.themeColorText) ui.themeColorText.value = hex;
};

const applyBackground = (value, persist = true) => {
  const path = (value || '').trim();
  currentBgImage = path;

  if (ui.bgImage) {
    const cssUrl = toCssUrl(path);
    ui.bgImage.style.backgroundImage = cssUrl ? `url("${cssUrl}")` : 'none';
  }
  if (ui.bgImageInput) ui.bgImageInput.value = path;

  if (persist) {
    localStorage.setItem('bgImage', path);
    post('set-background-image', { path });
  }
};

const handleThemeInput = (value) => {
  applyThemeColor(value);
  post('set-theme', { color: currentThemeColor });
};

const toggleModal = (el, show) => {
  if (!el) return;
  el.classList.toggle('show', show);
};

const clearTestSwitchCountdown = () => {
  if (testSwitchCountdownTimer) {
    clearInterval(testSwitchCountdownTimer);
    testSwitchCountdownTimer = null;
  }
};

const renderTestSwitchButton = () => {
  if (!ui.testSwitchButton) return;
  if (!isTestSwitchActive) {
    ui.testSwitchButton.dataset.state = 'test';
    ui.testSwitchButton.textContent = '测试切换';
    ui.testSwitchButton.classList.remove('danger');
    return;
  }

  const seconds = Math.max(0, Math.ceil(testSwitchRemainingSeconds));
  ui.testSwitchButton.dataset.state = 'restore';
  ui.testSwitchButton.textContent = `恢复原始分辨率 (${seconds}s)`;
  ui.testSwitchButton.classList.add('danger');
};

const startTestSwitchCountdown = (seconds) => {
  clearTestSwitchCountdown();
  testSwitchRemainingSeconds = Math.max(0, Number(seconds) || 0);
  renderTestSwitchButton();

  if (testSwitchRemainingSeconds <= 0) {
    return;
  }

  testSwitchCountdownTimer = setInterval(() => {
    testSwitchRemainingSeconds = Math.max(0, testSwitchRemainingSeconds - 1);
    renderTestSwitchButton();
    if (testSwitchRemainingSeconds <= 0) {
      clearTestSwitchCountdown();
    }
  }, 1000);
};

const setTestSwitchButtonState = (active, timeoutSeconds = 15) => {
  isTestSwitchActive = active;
  if (!active) {
    clearTestSwitchCountdown();
    testSwitchRemainingSeconds = 0;
    renderTestSwitchButton();
    return;
  }

  startTestSwitchCountdown(timeoutSeconds);
};

const updateMeta = () => {
  const startBat = ui.startBatSetting?.value || ui.startBat?.value || '未选择';
  if (ui.startBatHover) ui.startBatHover.textContent = startBat;
  if (ui.primaryDisplayHover && !ui.primaryDisplayHover.textContent.trim()) ui.primaryDisplayHover.textContent = '未选择';
  if (ui.originalModeHover && !ui.originalModeHover.textContent.trim()) ui.originalModeHover.textContent = '未读取';
};

onClick('btnLaunch', () => post('launch-game'));
onClick('btnTestSwitch', () => {
  if (isTestSwitchActive) {
    post('restore-original');
    return;
  }

  post('test-switch');
});
onClick('btnSettings', () => toggleModal(ui.settingsModal, true));
onClick('btnCloseSettings', () => toggleModal(ui.settingsModal, false));
onClick('btnApplyBg', () => applyBackground(ui.bgImageInput?.value || ''));
onClick('btnBrowseBg', () => post('pick-background-image'));

onClick('btnPickBat', () => post('pick-start-bat'));
onClick('btnDetectDisplays', () => post('detect-displays'));
onClick('btnSave', () => {
  const startBatPath = ui.startBat?.value || '';
  const primaryDisplay = ui.displaySelect?.value || '';
  if (!startBatPath.trim()) {
    setStatus('请先选择 start.bat', '#ff5a6a');
    return;
  }

  if (!isValidPrimaryDisplay(primaryDisplay)) {
    setStatus('请先选择主显示器', '#ff5a6a');
    return;
  }

  post('save-settings', {
    startBatPath,
    primaryDisplay,
    backgroundImagePath: ui.bgImageInput?.value || '',
  });

  toggleModal(ui.firstRun, false);
  updateMeta();
});

onClick('btnPickBatSetting', () => post('pick-start-bat'));
onClick('btnEditSegatoolsIni', () => post('open-segatools-ini'));
onClick('btnApplyRecommendedSegatools', () => post('apply-recommended-segatools-gfx'));
onClick('btnDetectDisplaysSetting', () => post('detect-displays'));
onClick('btnReadCurrentSetting', () => post('read-current-mode'));
onClick('btnSaveSettings', () => {
  post('save-settings', {
    startBatPath: ui.startBatSetting?.value || '',
    primaryDisplay: ui.displaySelectSetting?.value || '',
    originalMode: ui.originalModeInputSetting?.value || '',
    backgroundImagePath: ui.bgImageInput?.value || '',
  });

  toggleModal(ui.settingsModal, false);
  updateMeta();
});

if (ui.themeColor) ui.themeColor.addEventListener('input', (e) => handleThemeInput(e.target.value));
if (ui.themeColorText) ui.themeColorText.addEventListener('change', (e) => handleThemeInput(e.target.value));

const segs = document.querySelectorAll('#launchMode .seg');
segs.forEach((seg) => seg.addEventListener('click', () => {
  segs.forEach((s) => s.classList.remove('active'));
  seg.classList.add('active');
  post('set-launch-mode', { mode: seg.dataset.mode });
}));

if (ui.smartDisplayToggle) {
  ui.smartDisplayToggle.addEventListener('change', () => {
    post('set-smart-display', { enabled: !!ui.smartDisplayToggle.checked });
  });
}

const handleHostMessage = (event) => {
  const data = event.data || event;
  const { type, payload } = data || {};
  if (!type) return;

  switch (type) {
    case 'init': {
      if (payload.startBatPath) {
        if (ui.startBat) ui.startBat.value = payload.startBatPath;
        if (ui.startBatSetting) ui.startBatSetting.value = payload.startBatPath;
      }

      if (payload.originalMode) {
        if (ui.originalModeCard) ui.originalModeCard.textContent = payload.originalMode;
        if (ui.originalModeInputSetting) ui.originalModeInputSetting.value = payload.originalMode;
        if (ui.originalModeHover) ui.originalModeHover.textContent = payload.originalMode;
      }

      if (payload.targetMode) {
        if (ui.targetMode) ui.targetMode.textContent = payload.targetMode;
      }

      if (ui.smartDisplayToggle) {
        ui.smartDisplayToggle.checked = !!payload.smartDisplayEnabled;
      }

      if (payload.primaryDisplayName && ui.primaryDisplayHover) ui.primaryDisplayHover.textContent = payload.primaryDisplayName;
      if (payload.themeColor) applyThemeColor(payload.themeColor);
      if (payload.backgroundImagePath) applyBackground(payload.backgroundImagePath, false);
      if (payload.version && ui.appVersion) ui.appVersion.textContent = `v${payload.version}`;

      if (payload.displays) {
        if (ui.displaySelect) ui.displaySelect.innerHTML = '<option value="">请先选择</option>';
        if (ui.displaySelectSetting) ui.displaySelectSetting.innerHTML = '<option value="">请先选择</option>';
        payload.displays.forEach((d) => {
          const opt = document.createElement('option');
          opt.value = d.id;
          opt.textContent = d.name;
          if (d.selected) opt.selected = true;
          if (ui.displaySelect) ui.displaySelect.appendChild(opt.cloneNode(true));
          if (ui.displaySelectSetting) ui.displaySelectSetting.appendChild(opt);
        });
      }

      updateMeta();
      const needsFirstRun = !payload.startBatPath || !payload.primaryDisplayName || payload.primaryDisplayName === '未选择';
      toggleModal(ui.firstRun, needsFirstRun);
      break;
    }
    case 'status': {
      setStatus(payload.text || '待机', payload.color || '#7dffa0');
      break;
    }
    case 'update-original': {
      if (ui.originalModeCard) ui.originalModeCard.textContent = payload.value || '未读取';
      if (ui.originalModeInputSetting) ui.originalModeInputSetting.value = payload.value || '';
      if (ui.originalModeHover) ui.originalModeHover.textContent = payload.value || '未读取';
      break;
    }
    case 'update-target': {
      if (ui.targetMode) ui.targetMode.textContent = payload.value || '1920×1080 @ 120Hz';
      break;
    }
    case 'update-start-bat': {
      if (ui.startBat) ui.startBat.value = payload.path || '';
      if (ui.startBatSetting) ui.startBatSetting.value = payload.path || '';
      updateMeta();
      break;
    }
    case 'update-background-image': {
      applyBackground(payload.path || '', false);
      break;
    }
    case 'test-switch-state': {
      setTestSwitchButtonState(!!payload.active, payload.timeoutSeconds || 15);
      break;
    }
    default:
      console.log('Unknown message', type, payload);
  }
};

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener('message', handleHostMessage);
}
window.addEventListener('message', handleHostMessage);

setStatus('待机');
applyThemeColor(currentThemeColor);
applyBackground(currentBgImage, false);
setTestSwitchButtonState(false, 15);
