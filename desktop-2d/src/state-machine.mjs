export const ACTION_DURATIONS = Object.freeze({
  pickupStart: 640,
  pet: 1000,
  eat: 1000,
  lieDown: 4680,
  sleep: 1120,
  sleepBreathing: 10000,
  sleepReturn: 1120,
  restCurled: 960,
  restLoaf: 960,
  restFaceDown: 960,
  groom: 960,
  groomReturn: 960,
  landing: 800,
  edgeReturn: 800,
});

export const IDLE_ACTIONS = Object.freeze(["turn"]);
export const SPECIAL_IDLE_ACTIONS = Object.freeze([
  "lieDown",
  "sleep",
  "restCurled",
  "restLoaf",
  "restFaceDown",
  "groom",
]);
export const REST_ACTIONS = Object.freeze(["restCurled", "restLoaf", "restFaceDown"]);
export const REST_BREATHING_DURATION_MS = 10000;
export const GROOM_LOOP_MIN_DURATION_MS = 3000;
export const GROOM_LOOP_MAX_DURATION_MS = 8000;

export function randomGroomLoopDuration(random = Math.random) {
  const secondOffset = Math.max(0, Math.min(5, Math.floor(random() * 6)));
  return (3 + secondOffset) * 1000;
}

export function actionSequence(action, frameCount) {
  const forward = Array.from({ length: frameCount }, (_, index) => index);
  if (action === "lieDown" && frameCount > 1) {
    return [...forward, ...forward.slice(0, -1).reverse()];
  }
  return forward;
}

export function frameForElapsed(action, frameCount, frameMs, durationMs, elapsedMs) {
  if (frameCount <= 1) return 0;
  if (["walk", "idle", "pickedUp", "sleepBreathing"].includes(action)
      || action.endsWith("Loop")) {
    return Math.floor(elapsedMs / frameMs) % frameCount;
  }
  const forwardDuration = frameMs * (frameCount - 1);
  if (action === "lieDown") {
    if (elapsedMs < forwardDuration) return Math.min(frameCount - 1, Math.floor(elapsedMs / frameMs));
    const reverseStart = Math.max(forwardDuration, durationMs - forwardDuration);
    if (elapsedMs < reverseStart) return frameCount - 1;
    return Math.max(0, frameCount - 2 - Math.floor((elapsedMs - reverseStart) / frameMs));
  }
  return Math.min(frameCount - 1, Math.floor(elapsedMs / frameMs));
}

export function chooseIdleAction(random = Math.random, allowSpecial = false, enabledActions) {
  const isEnabled = (action) => enabledActions?.[action] !== false;
  const actions = [
    ...IDLE_ACTIONS,
    ...(allowSpecial ? SPECIAL_IDLE_ACTIONS.filter(isEnabled) : []),
  ];
  return actions[Math.min(actions.length - 1, Math.floor(random() * actions.length))];
}

export function isSpecialIdleAction(action) {
  return SPECIAL_IDLE_ACTIONS.includes(action);
}

export function isRestAction(action) {
  return REST_ACTIONS.includes(action);
}

export function specialIdleSequenceDuration(action) {
  if (!isSpecialIdleAction(action)) return 0;
  if (action === "lieDown") return ACTION_DURATIONS.lieDown;
  if (action === "sleep") {
    return ACTION_DURATIONS.sleep
      + ACTION_DURATIONS.sleepBreathing
      + ACTION_DURATIONS.sleepReturn;
  }
  if (isRestAction(action)) {
    return ACTION_DURATIONS[action] * 2 + REST_BREATHING_DURATION_MS;
  }
  if (action === "groom") {
    return ACTION_DURATIONS[action]
      + GROOM_LOOP_MAX_DURATION_MS
      + ACTION_DURATIONS.groomReturn;
  }
  return ACTION_DURATIONS[action]
    + ACTION_DURATIONS[`${action}Loop`]
    + ACTION_DURATIONS[`${action}Return`];
}

export function hasExceededDragThreshold(startX, startY, currentX, currentY, threshold = 6) {
  return Math.hypot(currentX - startX, currentY - startY) >= threshold;
}

export function nextDirection(direction) {
  return direction >= 0 ? -1 : 1;
}

export function nextActionAfterCompletion(action) {
  return {
    sleep: "sleepBreathing",
    sleepBreathing: "sleepReturn",
    sleepReturn: "walk",
    groom: "groomLoop",
    groomLoop: "groomReturn",
    groomReturn: "walk",
  }[action] ?? "walk";
}

export function clampPosition(x, spriteWidth, viewportWidth, padding = 12) {
  return Math.max(padding, Math.min(viewportWidth - spriteWidth - padding, x));
}

export function clampPetPosition(
  position,
  canvasWidth,
  canvasHeight,
  viewportWidth,
  viewportHeight,
  scale,
  padding = 12,
) {
  const transformOffset = canvasHeight * (1 - scale);
  return {
    x: clampPosition(position.x, canvasWidth * scale, viewportWidth, padding),
    y: Math.max(-transformOffset, Math.min(viewportHeight - canvasHeight, position.y)),
  };
}

export function advanceWrappedPosition(
  x,
  direction,
  deltaSeconds,
  baseSpeed,
  viewportWidth,
  visibleLeftOffset,
  visibleRightOffset,
  maxCrossingSeconds = 2.5,
) {
  const visibleWidth = visibleRightOffset - visibleLeftOffset;
  const isCrossing = x + visibleLeftOffset < 0 || x + visibleRightOffset > viewportWidth;
  const speed = isCrossing ? Math.max(baseSpeed, visibleWidth / maxCrossingSeconds) : baseSpeed;
  const next = x + direction * speed * deltaSeconds;
  if (direction > 0 && next + visibleLeftOffset >= viewportWidth) {
    return { x: -visibleRightOffset, wrapped: true };
  }
  if (direction < 0 && next + visibleRightOffset <= 0) {
    return { x: viewportWidth - visibleLeftOffset, wrapped: true };
  }
  return { x: next, wrapped: false };
}

export function isFullyOnScreen(x, viewportWidth, visibleLeftOffset, visibleRightOffset) {
  return x + visibleLeftOffset >= 0 && x + visibleRightOffset <= viewportWidth;
}

export function safeDropTarget(
  x,
  y,
  scale,
  canvasHeight,
  alphaBounds,
  viewportWidth,
  viewportHeight,
  padding = 12,
) {
  const bounds = alphaBounds ?? { left: 0, top: 0, right: 520, bottom: canvasHeight };
  const transformOffset = canvasHeight * (1 - scale);
  const leftOffset = bounds.left * scale;
  const rightOffset = bounds.right * scale;
  const topOffset = transformOffset + bounds.top * scale;
  const bottomOffset = transformOffset + bounds.bottom * scale;
  return {
    x: Math.max(padding - leftOffset, Math.min(viewportWidth - padding - rightOffset, x)),
    y: Math.max(padding - topOffset, Math.min(viewportHeight - padding - bottomOffset, y)),
  };
}

export function pixelsPerSecond(speed) {
  return 200 * Math.min(1.2, Math.max(0.2, speed));
}

export function isHeadRegion(x, y, bounds) {
  if (!bounds) return false;
  const width = bounds.right - bounds.left;
  const height = bounds.bottom - bounds.top;
  return x >= bounds.left + width * 0.68
    && x <= bounds.right
    && y >= bounds.top
    && y <= bounds.top + height * 0.58;
}
