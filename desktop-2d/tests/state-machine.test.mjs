import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {
  ACTION_DURATIONS,
  advanceWrappedPosition,
  actionSequence,
  chooseIdleAction,
  clampPosition,
  clampPetPosition,
  GROOM_LOOP_MAX_DURATION_MS,
  GROOM_LOOP_MIN_DURATION_MS,
  frameForElapsed,
  hasExceededDragThreshold,
  isFullyOnScreen,
  isHeadRegion,
  isRestAction,
  isSpecialIdleAction,
  nextActionAfterCompletion,
  nextDirection,
  pixelsPerSecond,
  randomGroomLoopDuration,
  safeDropTarget,
  safeRecoveryPosition,
  REST_BREATHING_DURATION_MS,
  specialIdleSequenceDuration,
} from "../src/state-machine.mjs";

test("lie-down plays forward and reverse while staged actions play once", () => {
  assert.deepEqual(actionSequence("lieDown", 8), [0, 1, 2, 3, 4, 5, 6, 7, 6, 5, 4, 3, 2, 1, 0]);
  assert.deepEqual(actionSequence("pickedUp", 8), [0, 1, 2, 3, 4, 5, 6, 7]);
});

test("lie-down holds its final pose for three seconds", () => {
  assert.equal(ACTION_DURATIONS.lieDown, 4680);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 839), 6);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 840), 7);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 3839), 7);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 3840), 6);
});

test("sleep breathing loop uses requested duration", () => {
  assert.equal(ACTION_DURATIONS.sleepBreathing, 10000);
  assert.equal(frameForElapsed("sleepBreathing", 8, 180, 10000, 1440), 0);
  assert.equal(nextActionAfterCompletion("sleep"), "sleepBreathing");
  assert.equal(nextActionAfterCompletion("sleepBreathing"), "sleepReturn");
  assert.equal(nextActionAfterCompletion("sleepReturn"), "walk");
});

test("rest variants hold with procedural breathing while grooming retains its loop", () => {
  for (const name of ["restCurled", "restLoaf", "restFaceDown"]) {
    assert.equal(isRestAction(name), true);
    assert.equal(ACTION_DURATIONS[`${name}Loop`], undefined);
    assert.equal(nextActionAfterCompletion(name), "walk");
    assert.equal(specialIdleSequenceDuration(name), 11920);
  }
  assert.equal(REST_BREATHING_DURATION_MS, 10000);
  assert.equal(nextActionAfterCompletion("groom"), "groomLoop");
  assert.equal(nextActionAfterCompletion("groomLoop"), "groomReturn");
  assert.equal(nextActionAfterCompletion("groomReturn"), "walk");
});

test("grooming loop uses eight independent frames at its source timing", () => {
  const manifest = JSON.parse(fs.readFileSync(
    new URL("../sprite-packs/orange-tabby/manifest.json", import.meta.url),
    "utf8",
  ));
  assert.deepEqual(manifest.actions.groomLoop.frames, [
    "frames/groomLoop-0.png",
    "frames/groomLoop-1.png",
    "frames/groomLoop-2.png",
    "frames/groomLoop-3.png",
    "frames/groomLoop-4.png",
    "frames/groomLoop-5.png",
    "frames/groomLoop-6.png",
    "frames/groomLoop-7.png",
  ]);
  assert.equal(manifest.actions.groomLoop.frameMs, 140);
  assert.equal(manifest.actions.groomLoop.loop, true);
  assert.equal(manifest.actions.groomLoop.frames.some(
    (frame) => manifest.actions.groom.frames.includes(frame),
  ), false);
  assert.equal(frameForElapsed("groomLoop", 8, 140, 3000, 0), 0);
  assert.equal(frameForElapsed("groomLoop", 8, 140, 3000, 139), 0);
  assert.equal(frameForElapsed("groomLoop", 8, 140, 3000, 140), 1);
  assert.equal(frameForElapsed("groomLoop", 8, 140, 3000, 1120), 0);
});

test("grooming loop duration is a random whole number of seconds from three to eight", () => {
  assert.equal(GROOM_LOOP_MIN_DURATION_MS, 3000);
  assert.equal(GROOM_LOOP_MAX_DURATION_MS, 8000);
  assert.equal(randomGroomLoopDuration(() => 0), 3000);
  assert.equal(randomGroomLoopDuration(() => 0.999999), 8000);
});

test("special idle actions report their animation sequence duration", () => {
  assert.equal(isSpecialIdleAction("restCurled"), true);
  assert.equal(isSpecialIdleAction("sleep"), true);
  assert.equal(isSpecialIdleAction("lieDown"), true);
  assert.equal(specialIdleSequenceDuration("groom"), 9920);
  assert.equal(specialIdleSequenceDuration("sleep"), 12240);
  assert.equal(specialIdleSequenceDuration("lieDown"), 4680);
});

test("picked-up animation loops while the cat is held", () => {
  assert.equal(frameForElapsed("pickedUp", 8, 100, Number.POSITIVE_INFINITY, 0), 0);
  assert.equal(frameForElapsed("pickedUp", 8, 100, Number.POSITIVE_INFINITY, 699), 6);
  assert.equal(frameForElapsed("pickedUp", 8, 100, Number.POSITIVE_INFINITY, 700), 7);
  assert.equal(frameForElapsed("pickedUp", 8, 100, Number.POSITIVE_INFINITY, 800), 0);
  assert.equal(frameForElapsed("pickedUp", 8, 100, Number.POSITIVE_INFINITY, 1700), 1);
});

test("pickup transition advances once before the held loop", () => {
  assert.equal(frameForElapsed("pickupStart", 8, 80, 640, 0), 0);
  assert.equal(frameForElapsed("pickupStart", 8, 80, 640, 559), 6);
  assert.equal(frameForElapsed("pickupStart", 8, 80, 640, 639), 7);
});

test("landing plays eight frames once before walking", () => {
  assert.equal(ACTION_DURATIONS.landing, 800);
  assert.equal(frameForElapsed("landing", 8, 100, 800, 0), 0);
  assert.equal(frameForElapsed("landing", 8, 100, 800, 699), 6);
  assert.equal(frameForElapsed("landing", 8, 100, 800, 799), 7);
  assert.equal(nextActionAfterCompletion("landing"), "walk");
});

test("petting head region excludes the body", () => {
  const bounds = { left: 100, top: 50, right: 400, bottom: 350 };
  assert.equal(isHeadRegion(350, 100, bounds), true);
  assert.equal(isHeadRegion(220, 180, bounds), false);
  assert.equal(isHeadRegion(350, 300, bounds), false);
});

test("idle action selection and direction are deterministic at boundaries", () => {
  assert.equal(chooseIdleAction(() => 0), "turn");
  assert.equal(chooseIdleAction(() => 0.999), "turn");
  assert.equal(chooseIdleAction(() => 0.5, true), "restCurled");
  assert.equal(chooseIdleAction(() => 0.999, true), "groom");
  assert.equal(nextDirection(1), -1);
  assert.equal(nextDirection(-1), 1);
});

test("disabled random actions are excluded while turning remains available", () => {
  const disabled = {
    lieDown: false,
    sleep: false,
    restCurled: false,
    restLoaf: false,
    restFaceDown: false,
    groom: false,
  };
  assert.equal(chooseIdleAction(() => 0, true, disabled), "turn");
  assert.equal(chooseIdleAction(() => 0.999, true, { ...disabled, groom: true }), "groom");
});

test("left click becomes a drag only after six pixels of movement", () => {
  assert.equal(hasExceededDragThreshold(100, 100, 104, 104), false);
  assert.equal(hasExceededDragThreshold(100, 100, 106, 100), true);
});

test("movement values remain inside supported ranges", () => {
  assert.equal(clampPosition(-20, 200, 1000), 12);
  assert.equal(clampPosition(900, 200, 1000), 788);
  assert.equal(pixelsPerSecond(0), 40);
  assert.equal(pixelsPerSecond(2), 240);
});

test("restored positions are clamped to the selected display viewport", () => {
  assert.deepEqual(
    clampPetPosition({ x: -50, y: 900 }, 520, 520, 1000, 700, 0.5),
    { x: 12, y: 180 },
  );
  assert.equal(clampPetPosition({ x: 50, y: -999 }, 520, 520, 1000, 700, 0.5).y, -260);
});

test("walking wraps at both screen edges and accelerates while crossing", () => {
  const right = advanceWrappedPosition(999, 1, 0.1, 100, 1000, 0, 200);
  assert.equal(right.wrapped, true);
  assert.equal(right.x, -200);
  const left = advanceWrappedPosition(-199, -1, 0.1, 100, 1000, 0, 200);
  assert.equal(left.wrapped, true);
  assert.equal(left.x, 1000);
  const crossing = advanceWrappedPosition(-100, 1, 1, 20, 1000, 0, 200);
  assert.equal(crossing.x, -20);
  assert.equal(isFullyOnScreen(100, 1000, 0, 200), true);
  assert.equal(isFullyOnScreen(-1, 1000, 0, 200), false);
});

test("drop targets move edge releases fully back on screen", () => {
  const target = safeDropTarget(950, 650, 0.5, 520,
    { left: 0, top: 60, right: 520, bottom: 500 }, 1000, 700);
  assert.deepEqual(target, { x: 728, y: 178 });
});

test("recovery position is centered with a visible bottom margin", () => {
  assert.deepEqual(safeRecoveryPosition(520, 520, 0.5, 1000, 700), { x: 370, y: 168 });
});
