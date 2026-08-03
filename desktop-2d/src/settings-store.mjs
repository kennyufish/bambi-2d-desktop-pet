import fs from "node:fs";
import path from "node:path";

export const RANDOM_ACTION_KEYS = Object.freeze([
  "sit",
  "lieDown",
  "sleep",
  "restCurled",
  "restLoaf",
  "restFaceDown",
  "groom",
]);

const DEFAULT_RANDOM_ACTIONS = Object.freeze(Object.fromEntries(
  RANDOM_ACTION_KEYS.map((action) => [action, true]),
));

export const DEFAULT_SETTINGS = Object.freeze({
  scale: 1,
  speed: 0.65,
  openAtLogin: false,
  randomActions: DEFAULT_RANDOM_ACTIONS,
});

export function sanitizeSettings(value = {}) {
  return {
    scale: clamp(Number(value.scale), 0.65, 1.35, DEFAULT_SETTINGS.scale),
    speed: clamp(Number(value.speed), 0.2, 1.2, DEFAULT_SETTINGS.speed),
    openAtLogin: Boolean(value.openAtLogin),
    randomActions: Object.fromEntries(RANDOM_ACTION_KEYS.map((action) => [
      action,
      typeof value.randomActions?.[action] === "boolean"
        ? value.randomActions[action]
        : DEFAULT_RANDOM_ACTIONS[action],
    ])),
  };
}

export function readSettings(filePath) {
  try {
    return sanitizeSettings(JSON.parse(fs.readFileSync(filePath, "utf8")));
  } catch {
    return sanitizeSettings(DEFAULT_SETTINGS);
  }
}

export function writeSettings(filePath, value) {
  const settings = sanitizeSettings(value);
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, `${JSON.stringify(settings, null, 2)}\n`, "utf8");
  return settings;
}

function clamp(value, minimum, maximum, fallback) {
  if (!Number.isFinite(value)) return fallback;
  return Math.min(maximum, Math.max(minimum, value));
}
