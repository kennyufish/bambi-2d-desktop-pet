import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  readPetState,
  sanitizePetState,
  selectPetDisplay,
  writePetState,
} from "../src/pet-state.mjs";

test("pet state persists position, rest, pause, and display", () => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), "desktop-pet-state-"));
  const file = path.join(directory, "pet-state.json");
  const saved = writePetState(file, {
    position: { x: 123.5, y: 456.25 },
    fixedRest: true,
    paused: true,
    displayId: 42,
  });
  assert.deepEqual(readPetState(file), saved);
  fs.rmSync(directory, { recursive: true, force: true });
});

test("invalid pet state falls back to safe defaults", () => {
  assert.deepEqual(sanitizePetState({
    position: { x: "bad", y: 20 },
    fixedRest: 1,
    paused: 0,
    displayId: "not-a-display",
  }), {
    position: null,
    fixedRest: true,
    paused: false,
    displayId: null,
  });
});

test("missing saved display falls back to the primary display", () => {
  const primary = { id: 1, bounds: { x: 0, y: 0, width: 1920, height: 1080 } };
  const displays = [primary, { id: 2, bounds: { x: 1920, y: 0, width: 1280, height: 1024 } }];
  assert.equal(selectPetDisplay(2, displays, primary).id, 2);
  assert.equal(selectPetDisplay(99, displays, primary).id, 1);
});
