const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("desktopPet", {
  getBootstrap: () => ipcRenderer.invoke("pet:get-bootstrap"),
  saveState: (state) => ipcRenderer.send("pet:state", state),
  setInteractive: (interactive) => ipcRenderer.send("pet:set-interactive", Boolean(interactive)),
  showActionMenu: () => ipcRenderer.send("pet:show-action-menu"),
  getSettings: () => ipcRenderer.invoke("settings:get"),
  saveSettings: (settings) => ipcRenderer.invoke("settings:save", settings),
  closeSettings: () => ipcRenderer.send("settings:close"),
  onCommand: (callback) => {
    const listener = (_event, command) => callback(command);
    ipcRenderer.on("pet:command", listener);
    return () => ipcRenderer.removeListener("pet:command", listener);
  },
  onSettings: (callback) => {
    const listener = (_event, settings) => callback(settings);
    ipcRenderer.on("settings:changed", listener);
    return () => ipcRenderer.removeListener("settings:changed", listener);
  },
  onPointerProbe: (callback) => {
    const listener = (_event, point) => callback(point);
    ipcRenderer.on("pet:pointer-probe", listener);
    return () => ipcRenderer.removeListener("pet:pointer-probe", listener);
  },
});
