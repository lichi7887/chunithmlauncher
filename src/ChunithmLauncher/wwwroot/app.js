const state = {
  config: null,
  monitors: [],
  version: ''
};

const ui = {
  summary: document.getElementById('summary'),
  version: document.getElementById('version'),
  runtimeStatus: document.getElementById('runtimeStatus'),
  wizardDialog: document.getElementById('wizardDialog'),
  settingsDialog: document.getElementById('settingsDialog')
};

function send(action, payload) {
  window.chrome.webview.postMessage(JSON.stringify({ action, payload }));
}

window.__hostResponse = (message) => {
  const parsed = typeof message === 'string' ? JSON.parse(message) : message;
  if (parsed.action === 'bootstrap') {
    const data = JSON.parse(parsed.result);
    state.config = data.config;
    state.monitors = data.monitors;
    state.version = data.version;
    hydrate();
    return;
  }

  if (parsed.result !== 'ok' && parsed.result !== 'unknown') {
    ui.runtimeStatus.textContent = parsed.result;
  }

  if (parsed.action === 'saveConfig' && parsed.result === 'ok') {
    ui.runtimeStatus.textContent = '设置已保存。';
    send('bootstrap');
  }
};

window.__runtimeStatus = (data) => {
  const payload = typeof data === 'string' ? JSON.parse(data) : data;
  ui.runtimeStatus.textContent = payload.message;
};

function hydrate() {
  const cfg = state.config;
  document.documentElement.style.setProperty('--theme', cfg.themeColorHex || '#fdd500');
  if (cfg.backgroundSource) {
    document.body.style.backgroundImage = `linear-gradient(rgba(9,9,16,.64), rgba(18,21,32,.85)), url('${cfg.backgroundSource}')`;
    document.body.style.backgroundSize = 'cover';
  }

  ui.version.textContent = `Version ${state.version}`;
  ui.summary.innerHTML = `
    <p><b>启动脚本</b><br>${cfg.startBatPath || '未配置'}</p>
    <p><b>主显示器</b><br>${monitorName(cfg.primaryMonitorId)}</p>
    <p><b>目标分辨率</b><br>${cfg.targetDisplayMode.width}x${cfg.targetDisplayMode.height}@${cfg.targetDisplayMode.refreshRate}Hz</p>
  `;

  bindSettings();
  if (!cfg.isFirstRunCompleted) {
    openWizard();
  }
}

function monitorName(id) {
  return state.monitors.find(m => m.id === id)?.description || '未选择';
}

function fillMonitorSelect(select, selectedId) {
  select.innerHTML = '';
  state.monitors.forEach(m => {
    const option = document.createElement('option');
    option.value = m.id;
    option.textContent = `${m.description}${m.isPrimary ? ' (系统主屏)' : ''}`;
    option.selected = m.id === selectedId;
    select.appendChild(option);
  });
}

function openWizard() {
  const cfg = state.config;
  const bat = document.getElementById('batPath');
  const monitorSelect = document.getElementById('monitorSelect');
  bat.value = cfg.startBatPath || '';
  fillMonitorSelect(monitorSelect, cfg.primaryMonitorId || state.monitors.find(x => x.isPrimary)?.id);

  document.getElementById('wizardSave').onclick = (ev) => {
    ev.preventDefault();
    cfg.startBatPath = bat.value.trim();
    cfg.primaryMonitorId = monitorSelect.value;
    const selectedMonitor = state.monitors.find(m => m.id === cfg.primaryMonitorId);
    cfg.originalDisplayMode = selectedMonitor?.currentMode || cfg.originalDisplayMode;
    cfg.isFirstRunCompleted = true;
    send('saveConfig', JSON.stringify(cfg));
    ui.wizardDialog.close();
  };

  ui.wizardDialog.showModal();
}

function bindSettings() {
  document.getElementById('openSettings').onclick = () => {
    const cfg = state.config;
    document.getElementById('setBat').value = cfg.startBatPath || '';
    document.getElementById('windowTitle').value = cfg.windowTitle || '';
    document.getElementById('targetW').value = cfg.targetDisplayMode.width;
    document.getElementById('targetH').value = cfg.targetDisplayMode.height;
    document.getElementById('targetR').value = cfg.targetDisplayMode.refreshRate;
    document.getElementById('themePicker').value = cfg.themeColorHex || '#fdd500';
    document.getElementById('themeHex').value = cfg.themeColorHex || '#fdd500';
    document.getElementById('bgSource').value = cfg.backgroundSource || '';
    fillMonitorSelect(document.getElementById('setMonitor'), cfg.primaryMonitorId);
    ui.settingsDialog.showModal();
  };

  document.getElementById('settingsSave').onclick = (ev) => {
    ev.preventDefault();
    const cfg = state.config;
    cfg.startBatPath = document.getElementById('setBat').value.trim();
    cfg.primaryMonitorId = document.getElementById('setMonitor').value;
    cfg.windowTitle = document.getElementById('windowTitle').value.trim() || 'teaGfx DirectX Release';
    cfg.targetDisplayMode.width = Number(document.getElementById('targetW').value || 1920);
    cfg.targetDisplayMode.height = Number(document.getElementById('targetH').value || 1080);
    cfg.targetDisplayMode.refreshRate = Number(document.getElementById('targetR').value || 120);
    cfg.themeColorHex = document.getElementById('themeHex').value.trim() || '#fdd500';
    cfg.backgroundSource = document.getElementById('bgSource').value.trim();

    document.documentElement.style.setProperty('--theme', cfg.themeColorHex);
    send('saveConfig', JSON.stringify(cfg));
    ui.settingsDialog.close();
  };

  document.getElementById('themePicker').oninput = (e) => {
    document.getElementById('themeHex').value = e.target.value;
    document.documentElement.style.setProperty('--theme', e.target.value);
  };

  document.getElementById('themeHex').oninput = (e) => {
    const hex = e.target.value.trim();
    if (/^#[0-9A-Fa-f]{6}$/.test(hex)) {
      document.getElementById('themePicker').value = hex;
      document.documentElement.style.setProperty('--theme', hex);
    }
  };

  document.getElementById('bgLocal').onchange = async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      document.getElementById('bgSource').value = reader.result;
    };
    reader.readAsDataURL(file);
  };

  document.getElementById('launchBtn').onclick = () => send('launch');
  document.getElementById('testBtn').onclick = () => send('testSwitch');
  document.getElementById('restoreBtn').onclick = () => send('restore');
}

send('bootstrap');
