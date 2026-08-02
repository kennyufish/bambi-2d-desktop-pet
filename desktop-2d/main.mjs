import { app, BrowserWindow, ipcMain, Menu, nativeImage, screen, Tray } from "electron";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { readSettings, writeSettings } from "./src/settings-store.mjs";

const appDirectory = path.dirname(fileURLToPath(import.meta.url));
const activePackId = "orange-tabby";
const manifestPath = path.join(appDirectory, "sprite-packs", activePackId, "manifest.json");
let petWindow;
let settingsWindow;
let tray;
let settings;
let pointerProbeTimer;

app.setName("YourCatDesktopPet");

function settingsPath() {
  return path.join(app.getPath("userData"), "settings.json");
}

function createPetWindow() {
  const bounds = screen.getPrimaryDisplay().bounds;
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
      height: 500,
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

function createTray() {
  const iconPath = path.join(appDirectory, "sprite-packs", activePackId, "frames", "idle-0.png");
  const icon = nativeImage.createFromPath(iconPath).resize({ width: 32, height: 32 });
  tray = new Tray(icon);
  tray.setToolTip("Your Cat Desktop Pet");
  const rebuild = (paused = false) => {
    tray.setContextMenu(Menu.buildFromTemplate([
      { label: "打开设置", click: createSettingsWindow },
      { type: "separator" },
      { label: paused ? "继续桌宠" : "暂停桌宠", click: () => { sendCommand("pause"); rebuild(!paused); } },
      { type: "separator" },
      { label: "尺寸 75%", click: () => setScale(0.75) },
      { label: "尺寸 100%", click: () => setScale(1) },
      { label: "尺寸 125%", click: () => setScale(1.25) },
      { type: "separator" },
      { label: "退出", click: () => app.quit() },
    ]));
  };
  rebuild(false);
  tray.on("double-click", createSettingsWindow);
}

app.whenReady().then(() => {
  settings = readSettings(settingsPath());
  app.setLoginItemSettings({ openAtLogin: settings.openAtLogin, path: process.execPath });
  createPetWindow();
  createTray();
  pointerProbeTimer = setInterval(() => {
    if (!petWindow || petWindow.isDestroyed()) return;
    const cursor = screen.getCursorScreenPoint();
    const bounds = petWindow.getBounds();
    petWindow.webContents.send("pet:pointer-probe", {
      x: cursor.x - bounds.x,
      y: cursor.y - bounds.y,
    });
  }, 33);
  screen.on("display-metrics-changed", () => {
    petWindow?.setBounds(screen.getPrimaryDisplay().bounds);
  });
});

app.on("window-all-closed", (event) => event.preventDefault());
app.on("before-quit", () => {
  clearInterval(pointerProbeTimer);
  tray?.destroy();
});

ipcMain.handle("pet:get-bootstrap", () => ({
  packId: activePackId,
  manifest: JSON.parse(fs.readFileSync(manifestPath, "utf8")),
  settings,
  display: screen.getPrimaryDisplay().bounds,
}));

ipcMain.on("pet:set-interactive", (_event, interactive) => {
  if (!petWindow || petWindow.isDestroyed()) return;
  petWindow.setIgnoreMouseEvents(!interactive, { forward: !interactive });
});

ipcMain.handle("settings:get", () => settings);
ipcMain.handle("settings:save", (_event, value) => {
  settings = writeSettings(settingsPath(), value);
  app.setLoginItemSettings({ openAtLogin: settings.openAtLogin, path: process.execPath });
  petWindow?.webContents.send("settings:changed", settings);
  return settings;
});
ipcMain.on("settings:close", () => settingsWindow?.close());
