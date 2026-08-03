import {
  ACTION_DURATIONS,
  advanceWrappedPosition,
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

const stage = document.querySelector("#stage");
const pet = document.querySelector("#pet");
const tilt = document.querySelector("#tilt");
const layers = [document.querySelector("#frame-a"), document.querySelector("#frame-b")];
const alphaCanvas = document.createElement("canvas");
const alphaContext = alphaCanvas.getContext("2d", { willReadFrequently: true });

let manifest;
let packId;
let settings;
let position = { x: 40, y: 0 };
let direction = 1;
let paused = false;
let dragging = false;
let pendingDrag;
let dragPointerId;
let dragOffset = { x: 0, y: 0 };
let dropRecovery;
let action = "walk";
let frameIndex = 0;
let currentLayer = 0;
let actionStartedAt = performance.now();
let nextFrameAt = 0;
let nextDirectionAt = performance.now() + 5000;
let nextIdleAt = performance.now() + randomIdleDelay();
let nextSpecialIdleAt = 0;
let previousTick = performance.now();
let currentImage;
let currentAlphaBounds;
let interactive = false;
let displayedFrameKey = "";
let requestedFrameKey = "";
let frameRequestId = 0;
let actionMenuOpen = false;

const bootstrap = await window.desktopPet.getBootstrap();
manifest = bootstrap.manifest;
packId = bootstrap.packId;
settings = bootstrap.settings;
pet.style.width = `${manifest.canvas.width}px`;
pet.style.height = `${manifest.canvas.height}px`;
position.y = window.innerHeight - manifest.canvas.height - 8;
applySettings();
await preloadFrames();
await showFrame("idle", 0, true);

window.desktopPet.onCommand(({ type, value }) => {
  if (type === "pause") {
    paused = !paused;
    pet.classList.toggle("paused", paused);
  }
  if (type === "menu-closed") closeActionMenu();
});
window.desktopPet.onSettings((value) => { settings = value; applySettings(); });
window.desktopPet.onPointerProbe(({ x, y }) => {
  if (!dragging && !pendingDrag) setInteractive(hitTest(x, y));
});

function frameUrl(actionName, index) {
  return new URL(`../sprite-packs/${packId}/${manifest.actions[actionName].frames[index]}`, document.baseURI).href;
}

async function preloadFrames() {
  const urls = [...new Set(Object.entries(manifest.actions).flatMap(([actionName, config]) =>
    config.frames.map((_, index) => frameUrl(actionName, index)),
  ))];
  await Promise.all(urls.map(async (url) => {
    const image = new Image();
    image.src = url;
    await image.decode();
  }));
}

async function showFrame(actionName, index, immediate = false) {
  const frameKey = `${actionName}:${index}`;
  if (frameKey === displayedFrameKey || frameKey === requestedFrameKey) return;
  requestedFrameKey = frameKey;
  const requestId = ++frameRequestId;
  const nextLayer = immediate ? currentLayer : 1 - currentLayer;
  const image = layers[nextLayer];
  image.src = frameUrl(actionName, index);
  try {
    await image.decode();
  } catch {
    if (!image.complete || !image.naturalWidth) {
      if (requestId === frameRequestId) requestedFrameKey = "";
      return;
    }
  }
  if (requestId !== frameRequestId) return;
  const previousLayer = currentLayer;
  image.classList.add("visible");
  currentLayer = nextLayer;
  currentImage = image;
  displayedFrameKey = frameKey;
  requestedFrameKey = "";
  updateAlphaMap(image);
  if (!immediate && previousLayer !== nextLayer) {
    requestAnimationFrame(() => {
      if (previousLayer !== currentLayer) layers[previousLayer].classList.remove("visible");
    });
  }
}

function startAction(name, now = performance.now()) {
  action = name;
  frameIndex = 0;
  actionStartedAt = now;
  nextFrameAt = now;
  pet.dataset.action = name;
  showFrame(name, 0);
}

function startRequestedAction(name, now = performance.now()) {
  if (dragging || pendingDrag || paused) return;
  if (name === "turn") {
    direction = nextDirection(direction);
    nextIdleAt = now + randomIdleDelay();
    return;
  }
  if (!manifest.actions[name]) return;
  if (isSpecialIdleAction(name)) {
    nextSpecialIdleAt = now + specialIdleSequenceDuration(name) + SPECIAL_IDLE_COOLDOWN_MS;
  }
  nextIdleAt = now + randomIdleDelay();
  startAction(name, now);
}

function updateAction(now) {
  const config = manifest.actions[action];
  const elapsed = now - actionStartedAt;
  if (now >= nextFrameAt) {
    const forwardDuration = config.frameMs * (config.frames.length - 1);
    frameIndex = actionMenuOpen && action === "lieDown" && elapsed >= forwardDuration
      ? config.frames.length - 1
      : frameForElapsed(
        action,
        config.frames.length,
        config.frameMs,
        ACTION_DURATIONS[action] ?? Number.POSITIVE_INFINITY,
        elapsed,
      );
    showFrame(action, frameIndex);
    nextFrameAt = now + config.frameMs;
  }
  if (dragging && action === "pickupStart" && elapsed >= ACTION_DURATIONS.pickupStart) {
    startAction("pickedUp", now);
  } else if (!dragging && !(actionMenuOpen && action === "lieDown")
      && Number.isFinite(ACTION_DURATIONS[action])
      && elapsed >= ACTION_DURATIONS[action]) {
    const successor = nextActionAfterCompletion(action);
    if (action === "edgeReturn" && dropRecovery) {
      position.x = dropRecovery.targetX;
      position.y = dropRecovery.targetY;
      dropRecovery = undefined;
    }
    if (successor === "walk") nextIdleAt = now + randomIdleDelay();
    startAction(successor, now);
  }
}

function tick(now) {
  const deltaSeconds = Math.min(0.05, (now - previousTick) / 1000);
  previousTick = now;
  if (!paused) {
    if (dropRecovery && action === "edgeReturn") updateDropRecovery(now);
    updateAction(now);
    if (!dragging && action === "walk") {
      const scale = effectiveScale();
      const leftOffset = (currentAlphaBounds?.left ?? 0) * scale;
      const rightOffset = (currentAlphaBounds?.right ?? manifest.canvas.width) * scale;
      const movement = advanceWrappedPosition(
        position.x,
        direction,
        deltaSeconds,
        pixelsPerSecond(settings.speed),
        window.innerWidth,
        leftOffset,
        rightOffset,
      );
      position.x = movement.x;
      if (movement.wrapped) {
        nextDirectionAt = now + 5000;
        nextIdleAt = now + randomIdleDelay();
      }
      const fullyOnScreen = isFullyOnScreen(position.x, window.innerWidth, leftOffset, rightOffset);
      if (fullyOnScreen && now >= nextDirectionAt) {
        direction = nextDirection(direction);
        nextDirectionAt = now + 5000;
      }
      if (fullyOnScreen && now >= nextIdleAt) {
        const idleAction = chooseIdleAction(
          Math.random,
          now >= nextSpecialIdleAt,
          settings.randomActions,
        );
        if (idleAction === "turn") direction = nextDirection(direction);
        else {
          if (isSpecialIdleAction(idleAction)) {
            nextSpecialIdleAt = now
              + specialIdleSequenceDuration(idleAction)
              + SPECIAL_IDLE_COOLDOWN_MS;
          }
          startAction(idleAction, now);
        }
        nextIdleAt = now + randomIdleDelay();
      }
    }
  }
  renderPosition();
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);

function renderPosition() {
  pet.style.setProperty("--x", `${position.x}px`);
  pet.style.setProperty("--y", `${position.y}px`);
  pet.style.setProperty("--facing", String(direction));
}

function applySettings() {
  pet.style.setProperty("--pet-scale", String(effectiveScale()));
  position.x = clampPosition(position.x, manifest.canvas.width * effectiveScale(), window.innerWidth);
  position.y = clampVerticalPosition(position.y);
}

stage.addEventListener("pointermove", (event) => {
  if (pendingDrag && event.pointerId === pendingDrag.pointerId) {
    if (hasExceededDragThreshold(
      pendingDrag.startX,
      pendingDrag.startY,
      event.clientX,
      event.clientY,
    )) beginDrag(event);
    setInteractive(true);
    return;
  }
  if (dragging) {
    positionForDragCursor(event.clientX, event.clientY);
    setInteractive(true);
    return;
  }
  const inside = hitTest(event.clientX, event.clientY);
  setInteractive(inside);
  if (inside) {
    const rect = pet.getBoundingClientRect();
    const relative = (event.clientX - rect.left) / rect.width - 0.5;
    tilt.style.setProperty("--tilt-y", `${relative * 7}deg`);
    tilt.style.setProperty("--tilt-z", `${relative * 1.5}deg`);
  } else {
    tilt.style.setProperty("--tilt-y", "0deg");
    tilt.style.setProperty("--tilt-z", "0deg");
  }
});

stage.addEventListener("pointerdown", (event) => {
  if (event.button !== 0 || !hitTest(event.clientX, event.clientY)) return;
  pendingDrag = {
    pointerId: event.pointerId,
    startX: event.clientX,
    startY: event.clientY,
  };
  stage.setPointerCapture(event.pointerId);
  setInteractive(true);
});

stage.addEventListener("pointerup", (event) => {
  if (pendingDrag?.pointerId === event.pointerId) {
    pendingDrag = undefined;
    releasePointer(event.pointerId);
    setInteractive(hitTest(event.clientX, event.clientY));
    return;
  }
  if (!dragging || dragPointerId !== event.pointerId) return;
  positionForDragCursor(event.clientX, event.clientY);
  dragging = false;
  dragPointerId = undefined;
  pet.classList.remove("dragging");
  releasePointer(event.pointerId);
  const target = safeDropTarget(
    position.x,
    position.y,
    effectiveScale(),
    manifest.canvas.height,
    currentAlphaBounds && { ...currentAlphaBounds, left: 0, right: manifest.canvas.width },
    window.innerWidth,
    window.innerHeight,
  );
  if (Math.hypot(target.x - position.x, target.y - position.y) > 1) {
    dropRecovery = {
      startX: position.x,
      startY: position.y,
      targetX: target.x,
      targetY: target.y,
      startedAt: performance.now(),
      lift: target.y > position.y + 1 ? 0 : 48,
    };
    if (Math.abs(target.x - position.x) > 1) direction = target.x > position.x ? 1 : -1;
    startAction("edgeReturn", dropRecovery.startedAt);
  } else {
    startAction("landing");
  }
});

stage.addEventListener("contextmenu", (event) => {
  if (actionMenuOpen || !hitTest(event.clientX, event.clientY)) return;
  event.preventDefault();
  actionMenuOpen = true;
  startAction("lieDown");
  window.desktopPet.showActionMenu();
});

stage.addEventListener("dblclick", (event) => {
  if (event.button !== 0 || !headHitTest(event.clientX, event.clientY)) return;
  event.preventDefault();
  startRequestedAction("pet");
});

window.addEventListener("resize", applySettings);

function updateAlphaMap(image) {
  if (!image.naturalWidth) return;
  alphaCanvas.width = image.naturalWidth;
  alphaCanvas.height = image.naturalHeight;
  alphaContext.clearRect(0, 0, alphaCanvas.width, alphaCanvas.height);
  alphaContext.drawImage(image, 0, 0);
  currentAlphaBounds = alphaBounds();
}

function alphaBounds() {
  const { width, height } = alphaCanvas;
  if (!width || !height) return undefined;
  const data = alphaContext.getImageData(0, 0, width, height).data;
  let left = width;
  let top = height;
  let right = -1;
  let bottom = -1;
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      if (data[(y * width + x) * 4 + 3] <= 24) continue;
      left = Math.min(left, x);
      top = Math.min(top, y);
      right = Math.max(right, x);
      bottom = Math.max(bottom, y);
    }
  }
  return right < 0 ? undefined : { left, top, right: right + 1, bottom: bottom + 1 };
}

function hitTest(clientX, clientY) {
  const rect = pet.getBoundingClientRect();
  if (clientX < rect.left || clientX > rect.right || clientY < rect.top || clientY > rect.bottom) return false;
  if (!currentImage?.naturalWidth || !alphaCanvas.width) return true;
  let normalizedX = (clientX - rect.left) / rect.width;
  if (direction < 0) normalizedX = 1 - normalizedX;
  const x = Math.max(0, Math.min(alphaCanvas.width - 1, Math.floor(normalizedX * alphaCanvas.width)));
  const y = Math.max(0, Math.min(alphaCanvas.height - 1, Math.floor(((clientY - rect.top) / rect.height) * alphaCanvas.height)));
  try { return alphaContext.getImageData(x, y, 1, 1).data[3] > 24; }
  catch { return true; }
}

function headHitTest(clientX, clientY) {
  const point = imagePoint(clientX, clientY);
  return point && hitTest(clientX, clientY) && isHeadRegion(point.x, point.y, currentAlphaBounds);
}

function imagePoint(clientX, clientY) {
  const rect = pet.getBoundingClientRect();
  if (clientX < rect.left || clientX > rect.right || clientY < rect.top || clientY > rect.bottom) return undefined;
  let normalizedX = (clientX - rect.left) / rect.width;
  if (direction < 0) normalizedX = 1 - normalizedX;
  return {
    x: Math.max(0, Math.min(alphaCanvas.width - 1, Math.floor(normalizedX * alphaCanvas.width))),
    y: Math.max(0, Math.min(alphaCanvas.height - 1, Math.floor(((clientY - rect.top) / rect.height) * alphaCanvas.height))),
  };
}

function positionForDragCursor(clientX, clientY) {
  const scale = effectiveScale();
  const anchorX = direction < 0 ? manifest.canvas.width - dragOffset.x : dragOffset.x;
  const transformOffset = manifest.canvas.height * (1 - scale);
  position.x = clientX - anchorX * scale;
  position.y = clientY - dragOffset.y * scale - transformOffset;
}

function beginDrag(event) {
  pendingDrag = undefined;
  dragging = true;
  dragPointerId = event.pointerId;
  pet.classList.add("dragging");
  startAction("pickupStart");
  dragOffset = manifest.actions.pickupStart.dragAnchor;
  positionForDragCursor(event.clientX, event.clientY);
}

function releasePointer(pointerId) {
  if (stage.hasPointerCapture(pointerId)) stage.releasePointerCapture(pointerId);
}

function updateDropRecovery(now) {
  const elapsed = now - dropRecovery.startedAt;
  const progress = Math.min(1, elapsed / ACTION_DURATIONS.edgeReturn);
  const eased = 1 - (1 - progress) ** 3;
  position.x = dropRecovery.startX + (dropRecovery.targetX - dropRecovery.startX) * eased;
  position.y = dropRecovery.startY + (dropRecovery.targetY - dropRecovery.startY) * eased
    - Math.sin(Math.PI * progress) * dropRecovery.lift;
}

function setInteractive(value) {
  if (interactive === value) return;
  interactive = value;
  window.desktopPet.setInteractive(value);
}

function clampVerticalPosition(y) {
  const scaledHeight = manifest.canvas.height * effectiveScale();
  const transformOffset = manifest.canvas.height - scaledHeight;
  return Math.max(-transformOffset, Math.min(window.innerHeight - manifest.canvas.height, y));
}

function effectiveScale() {
  return settings.scale * (manifest.canvas.displayScale ?? 1);
}

function randomIdleDelay() { return 6000 + Math.random() * 4000; }

function closeActionMenu(now = performance.now()) {
  if (!actionMenuOpen) return;
  actionMenuOpen = false;
  nextIdleAt = now + randomIdleDelay();
  if (action === "lieDown") {
    const config = manifest.actions.lieDown;
    const forwardDuration = config.frameMs * (config.frames.length - 1);
    if (now - actionStartedAt >= forwardDuration) {
      actionStartedAt = now - (ACTION_DURATIONS.lieDown - forwardDuration);
      nextFrameAt = now;
    }
  }
}
