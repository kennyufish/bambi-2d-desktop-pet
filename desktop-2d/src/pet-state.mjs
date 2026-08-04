import fs from "node:fs";
import path from "node:path";

export const DEFAULT_PET_STATE = Object.freeze({
  position: null,
  fixedRest: false,
  paused: false,
  displayId: null,
});

export function sanitizePetState(value = {}) {
  const source = value && typeof value === "object" ? value : {};
  const x = Number(source.position?.x);
  const y = Number(source.position?.y);
  const displayId = Number(source.displayId);
  return {
    position: Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null,
    fixedRest: Boolean(source.fixedRest),
    paused: Boolean(source.paused),
    displayId: Number.isSafeInteger(displayId) && displayId >= 0 ? displayId : null,
  };
}

export function readPetState(filePath) {
  try {
    return sanitizePetState(JSON.parse(fs.readFileSync(filePath, "utf8")));
  } catch {
    return sanitizePetState(DEFAULT_PET_STATE);
  }
}

export function writePetState(filePath, value) {
  const state = sanitizePetState(value);
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(state, null, 2)}\n`, "utf8");
  return state;
}

export function selectPetDisplay(savedDisplayId, displays, primaryDisplay) {
  return displays.find((display) => display.id === savedDisplayId) ?? primaryDisplay;
}
