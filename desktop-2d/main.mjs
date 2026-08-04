import {
  app,
  BrowserWindow,
  globalShortcut,
  ipcMain,
  Menu,
  nativeImage,
  screen,
  Tray,
} from "electron";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { RANDOM_ACTION_KEYS, readSettings, writeSettings } from "./src/settings-store.mjs";
import { clampPetPosition, safeRecoveryPosition } from "./src/state-machine.mjs";
import {
  readPetState,
  sanitizePetState,
  selectPetDisplay,
  writePetState,
} from "./src/pet-state.mjs";

const appDirectory = path.dirname(fileURLToPath(import.meta.url));
const activePackId = "orange-tabby";
const manifestPath = path.join(appDirectory, "sprite-packs", activePackId, "manifest.json");
const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
const PET_STATE_WRITE_DELAY_MS = 500;
let petWindow;
let settingsWindow;
let tray;
let settings;
let petState;
let pointerProbeTimer;
let stateWriteTimer;
let activeDisplayId;
let rebuildTray;

app.setName("YourCatDesktopPet");

function settingsPath() {
  return path.join(app.getPath("userData"), "settings.json");
}

function petStatePath() {
  return path.join(app.getPath("userData"), "pet-state.json");
}

function schedulePetStateWrite() {
  if (stateWriteTimer) return;
  stateWriteTimer = setTimeout(() => {
    stateWriteTimer = undefined;
    writePetState(petStatePath(), petState);
  }, PET_STATE_WRITE_DELAY_MS);
}

function flushPetState() {
  if (stateWriteTimer) {
    clearTimeout(stateWriteTimer);
    stateWriteTimer = undefined;
  }
  if (petState) writePetState(petStatePath(), petState);
}

function updatePetState(value) {
  const next = sanitizePetState({ ...petState, ...value });
  if (value.position !== undefined && !next.position) return;
  petState = next;
  schedulePetStateWrite();
}

function effectiveScale() {
  return settings.scale * (manifest.canvas.displayScale ?? 1);
}

function defaultPosition(display) {
  return {
    x: 40,
    y: display.bounds.height - manifest.canvas.height - 8,
  };
}

function positionForDisplay(display, position = petState.position) {
  return clampPetPosition(
    position ?? defaultPosition(display),
    manifest.canvas.width,
    manifest.canvas.height,
    display.bounds.width,
    display.bounds.height,
    effectiveScale(),
  );
}

function setActiveDisplay(display, position = petState.position) {
  const nextPosition = positionForDisplay(display, position);
  activeDisplayId = display.id;
  updatePetState({ displayId: display.id, position: nextPosition });
  return nextPosition;
}

function placePetWindow(display, position = petState.position) {
  const nextPosition = setActiveDisplay(display, position);
  petWindow?.setBounds(display.bounds);
  sendCommand("set-position", nextPosition);
}

function findCat() {
  if (!petWindow || petWindow.isDestroyed()) return;
  const display = screen.getDisplayNearestPoint(screen.getCursorScreenPoint());
  const position = safeRecoveryPosition(
    manifest.canvas.width,
    manifest.canvas.height,
    effectiveScale(),
    display.bounds.width,
    display.bounds.height,
  );
  placePetWindow(display, position);
}

function currentPetDisplay() {
  return selectPetDisplay(
    activeDisplayId ?? petState.displayId,
    screen.getAllDisplays(),
    screen.getPrimaryDisplay(),
  );
}

function createPetWindow() {
  const display = currentPetDisplay();
  setActiveDisplay(display);
  const bounds = display.bounds;
  petWindow = new BrowserWindow({
    ...bounds,
    transparent: true,
    frame: false,
    resizable: false,
    movable: false,
    alwaysOnTop: true,
    skipTaskbar: true,
    fullscreenable: false,
    hasShadow: false,
    backgroundColor: "#00000000",
    webPreferences: {
      preload: path.join(appDirectory, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });
  petWindow.setAlwaysOnTop(true, "screen-saver");
  petWindow.setIgnoreMouseEvents(true, { forward: true });
  petWindow.loadFile(path.join(appDirectory, "renderer", "pet.html"));
  petWindow.on("closed", () => { petWindow = undefined; });
}

function createSettingsWindow() {
  if (settingsWindow && !settingsWindow.isDestroyed()) {
    settingsWindow.show();
    settingsWindow.focus();
    return;
  }
  settingsWindow = new BrowserWindow({
      width: 420,
      height: 560,
    resizable: false,
    maximizable: false,
    minimizable: false,
    title: "你家猫桌宠设置",
    alwaysOnTop: true,
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(appDirectory, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });
  settingsWindow.loadFile(path.join(appDirectory, "renderer", "settings.html"));
  settingsWindow.on("closed", () => { settingsWindow = undefined; });
}

function sendCommand(type, value) {
  petWindow?.webContents.send("pet:command", { type, value });
}

function setScale(scale) {
  settings = writeSettings(settingsPath(), { ...settings, scale });
  petWindow?.webContents.send("settings:changed", settings);
}

function setRandomAction(action, enabled) {
  if (!RANDOM_ACTION_KEYS.includes(action)) return;
  settings = writeSettings(settingsPath(), {
    ...settings,
    randomActions: { ...settings.randomActions, [action]: enabled },
  });
  petWindow?.webContents.send("settings:changed", settings);
}

function setPaused(value) {
  updatePetState({ paused: Boolean(value) });
  sendCommand("set-paused", petState.paused);
  rebuildTray?.();
}

function setFixedRest(value) {
  updatePetState({ fixedRest: Boolean(value) });
  sendCommand("set-fixed-rest", petState.fixedRest);
}

function createTray() {
  const iconPath = path.join(appDirectory, "sprite-packs", activePackId, "frames", "idle-0.png");
  const icon = nativeImage.createFromPath(iconPath).resize({ width: 32, height: 32 });
  tray = new Tray(icon);
  tray.setToolTip("Your Cat Desktop Pet");
  rebuildTray = () => {
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: "打开设置", click: createSettingsWindow },
      { label: "找回猫咪", click: findCat },
      { type: "separator" },
      {
        label: petState.paused ? "继续桌宠" : "暂停桌宠",
        click: () => setPaused(!petState.paused),
      },
      { type: "separator" },
      { label: "尺寸 75%", click: () => setScale(0.75) },
      { label: "尺寸 100%", click: () => setScale(1) },
      { label: "尺寸 125%", click: () => setScale(1.25) },
      { type: "separator" },
      { label: "退出", click: () => app.quit() },
    ]));
  };
  rebuildTray();
  tray.on("double-click", createSettingsWindow);
}

app.whenReady().then(() => {
  settings = readSettings(settingsPath());
  petState = readPetState(petStatePath());
  app.setLoginItemSettings({ openAtLogin: settings.openAtLogin, path: process.execPath });
  createPetWindow();
  createTray();
  globalShortcut.register("Control+Shift+H", findCat);
  pointerProbeTimer = setInterval(() => {
    if (!petWindow || petWindow.isDestroyed()) return;
    const cursor = screen.getCursorScreenPoint();
    const bounds = petWindow.getBounds();
    petWindow.webContents.send("pet:pointer-probe", {
      x: cursor.x - bounds.x,
      y: cursor.y - bounds.y,
    });
  }, 33);
  const syncPetDisplay = () => {
    if (!petWindow || petWindow.isDestroyed()) return;
    placePetWindow(currentPetDisplay());
  };
  screen.on("display-metrics-changed", syncPetDisplay);
  screen.on("display-removed", syncPetDisplay);
});

app.on("window-all-closed", (event) => event.preventDefault());
app.on("before-quit", () => {
  clearInterval(pointerProbeTimer);
  globalShortcut.unregister("Control+Shift+H");
  flushPetState();
  tray?.destroy();
});

ipcMain.handle("pet:get-bootstrap", () => ({
  packId: activePackId,
  manifest,
  settings,
  petState,
  display: currentPetDisplay().bounds,
}));

ipcMain.on("pet:set-interactive", (_event, interactive) => {
  if (!petWindow || petWindow.isDestroyed()) return;
  petWindow.setIgnoreMouseEvents(!interactive, { forward: !interactive });
});

ipcMain.on("pet:state", (event, value) => {
  if (!petWindow || petWindow.isDestroyed() || event.sender !== petWindow.webContents) return;
  updatePetState({ position: value?.position });
});

ipcMain.on("pet:show-action-menu", (event) => {
  if (!petWindow || petWindow.isDestroyed() || event.sender !== petWindow.webContents) return;
  const actions = [
    ["lieDown", "趴下"],
    ["sleep", "睡觉"],
    ["restCurled", "休息 1（蜷缩）"],
    ["restLoaf", "休息 2（香箱）"],
    ["restFaceDown", "休息 3（趴睡）"],
    ["groom", "舔毛"],
  ];
  const menu = Menu.buildFromTemplate([
    { label: "随机动作开关", enabled: false },
    { type: "separator" },
    ...actions.map(([action, label]) => ({
      label,
      type: "checkbox",
      checked: settings.randomActions[action],
      click: (item) => setRandomAction(action, item.checked),
    })),
    { type: "separator" },
    {
      label: "固定休息",
      type: "checkbox",
      checked: petState.fixedRest,
      click: (item) => setFixedRest(item.checked),
    },
    { type: "separator" },
    { label: "打开设置", click: createSettingsWindow },
    { label: "退出程序", click: () => app.quit() },
  ]);
  menu.popup({
    window: petWindow,
    callback: () => sendCommand("menu-closed"),
  });
});

ipcMain.handle("settings:get", () => settings);
ipcMain.handle("settings:save", (_event, value) => {
  settings = writeSettings(settingsPath(), { ...settings, ...value });
  app.setLoginItemSettings({ openAtLogin: settings.openAtLogin, path: process.execPath });
  petWindow?.webContents.send("settings:changed", settings);
  return settings;
});
ipcMain.on("settings:close", () => settingsWindow?.close());
