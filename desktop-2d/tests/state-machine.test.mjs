import test from "node:test";
import assert from "node:assert/strict";
import {
  ACTION_DURATIONS,
  advanceWrappedPosition,
  actionSequence,
  chooseIdleAction,
  clampPosition,
  frameForElapsed,
  hasExceededDragThreshold,
  isFullyOnScreen,
  isHeadRegion,
  isSpecialIdleAction,
  nextActionAfterCompletion,
  nextDirection,
  pixelsPerSecond,
  safeDropTarget,
  SPECIAL_IDLE_COOLDOWN_MS,
  specialIdleSequenceDuration,
} from "../src/state-machine.mjs";

test("lie-down plays forward and reverse while staged actions play once", () => {
  assert.deepEqual(actionSequence("lieDown", 8), [0, 1, 2, 3, 4, 5, 6, 7, 6, 5, 4, 3, 2, 1, 0]);
  assert.deepEqual(actionSequence("sit", 8), [0, 1, 2, 3, 4, 5, 6, 7]);
  assert.deepEqual(actionSequence("pickedUp", 8), [0, 1, 2, 3, 4, 5, 6, 7]);
});

test("lie-down holds its final pose for three seconds", () => {
  assert.equal(ACTION_DURATIONS.lieDown, 4680);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 839), 6);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 840), 7);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 3839), 7);
  assert.equal(frameForElapsed("lieDown", 8, 120, ACTION_DURATIONS.lieDown, 3840), 6);
});

test("sleep breathing and sitting tail loops use requested durations", () => {
  assert.equal(ACTION_DURATIONS.sit, 960);
  assert.equal(ACTION_DURATIONS.sleepBreathing, 10000);
  assert.equal(ACTION_DURATIONS.sitTail, 5000);
  assert.equal(frameForElapsed("sit", 16, 60, ACTION_DURATIONS.sit, 899), 14);
  assert.equal(frameForElapsed("sit", 16, 60, ACTION_DURATIONS.sit, 959), 15);
  assert.equal(frameForElapsed("sleepBreathing", 8, 180, 10000, 1440), 0);
  assert.equal(frameForElapsed("sitTail", 8, 140, 5000, 1120), 0);
  assert.equal(nextActionAfterCompletion("sit"), "sitTail");
  assert.equal(nextActionAfterCompletion("sitTail"), "sitReturn");
  assert.equal(nextActionAfterCompletion("sleep"), "sleepBreathing");
  assert.equal(nextActionAfterCompletion("sleepBreathing"), "sleepReturn");
  assert.equal(nextActionAfterCompletion("sleepReturn"), "walk");
});

test("three rest variants and grooming enter, loop, then return to walking", () => {
  for (const [name, loopDuration] of [
    ["restCurled", 10000],
    ["restLoaf", 10000],
    ["restFaceDown", 10000],
    ["groom", 5000],
  ]) {
    assert.equal(nextActionAfterCompletion(name), `${name}Loop`);
    assert.equal(nextActionAfterCompletion(`${name}Loop`), `${name}Return`);
    assert.equal(nextActionAfterCompletion(`${name}Return`), "walk");
    assert.equal(ACTION_DURATIONS[`${name}Loop`], loopDuration);
    assert.equal(frameForElapsed(`${name}Loop`, 8, 100, loopDuration, 800), 0);
  }
});

test("special idle actions share a five-minute post-animation cooldown", () => {
  assert.equal(SPECIAL_IDLE_COOLDOWN_MS, 300000);
  assert.equal(isSpecialIdleAction("restCurled"), true);
  assert.equal(isSpecialIdleAction("sleep"), true);
  assert.equal(isSpecialIdleAction("sit"), false);
  assert.equal(specialIdleSequenceDuration("restCurled"), 11920);
  assert.equal(specialIdleSequenceDuration("groom"), 6920);
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
  assert.equal(chooseIdleAction(() => 0), "sit");
  assert.equal(chooseIdleAction(() => 0.999), "turn");
  assert.equal(chooseIdleAction(() => 0.5, true), "restCurled");
  assert.equal(chooseIdleAction(() => 0.999, true), "groom");
  assert.equal(nextDirection(1), -1);
  assert.equal(nextDirection(-1), 1);
});

test("disabled random actions are excluded while turning remains available", () => {
  const disabled = {
    sit: false,
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
