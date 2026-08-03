import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { readSettings, sanitizeSettings, writeSettings } from "../src/settings-store.mjs";

test("settings are clamped and persisted", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "desktop-pet-settings-"));
  const file = path.join(directory, "settings.json");
  const saved = writeSettings(file, { scale: 5, speed: -1, openAtLogin: true });
  assert.equal(saved.scale, 1.35);
  assert.equal(saved.speed, 0.2);
  assert.equal(saved.openAtLogin, true);
  assert.equal(saved.specialIdleCooldownMs, 10000);
  assert.equal(Object.values(saved.randomActions).every(Boolean), true);
  assert.deepEqual(readSettings(file), saved);
  fs.rmSync(directory, { recursive: true, force: true });
});

test("invalid values restore defaults", () => {
  assert.deepEqual(sanitizeSettings({ scale: "bad", speed: null }), {
    scale: 1,
    speed: 0.2,
    openAtLogin: false,
    specialIdleCooldownMs: 10000,
    randomActions: {
      lieDown: true,
      sleep: true,
      restCurled: true,
      restLoaf: true,
      restFaceDown: true,
      groom: true,
    },
  });
});

test("random action switches persist independently", () => {
  const settings = sanitizeSettings({
    randomActions: { groom: false },
  });
  assert.equal(settings.randomActions.groom, false);
  assert.equal(settings.randomActions.sleep, true);
});

test("special animation cooldown is limited to five seconds through ten minutes", () => {
  assert.equal(sanitizeSettings({ specialIdleCooldownMs: 1 }).specialIdleCooldownMs, 5000);
  assert.equal(sanitizeSettings({ specialIdleCooldownMs: 900000 }).specialIdleCooldownMs, 600000);
});
