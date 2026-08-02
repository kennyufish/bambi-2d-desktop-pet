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
  assert.deepEqual(saved, { scale: 1.35, speed: 0.2, openAtLogin: true });
  assert.deepEqual(readSettings(file), saved);
  fs.rmSync(directory, { recursive: true, force: true });
});

test("invalid values restore defaults", () => {
  assert.deepEqual(sanitizeSettings({ scale: "bad", speed: null }), {
    scale: 1,
    speed: 0.2,
    openAtLogin: false,
  });
});
