const form = document.querySelector("#settings-form");
const scale = document.querySelector("#scale");
const speed = document.querySelector("#speed");
const startup = document.querySelector("#startup");
const scaleOutput = document.querySelector("#scale-output");
const speedOutput = document.querySelector("#speed-output");
const cooldown = document.querySelector("#cooldown");
const cooldownSeconds = document.querySelector("#cooldown-seconds");
const cooldownOutput = document.querySelector("#cooldown-output");

const settings = await window.desktopPet.getSettings();
scale.value = settings.scale;
speed.value = settings.speed;
cooldown.value = Math.round(settings.specialIdleCooldownMs / 1000);
cooldownSeconds.value = cooldown.value;
startup.checked = settings.openAtLogin;
updateOutputs();

scale.addEventListener("input", updateOutputs);
speed.addEventListener("input", updateOutputs);
cooldown.addEventListener("input", () => {
  cooldownSeconds.value = cooldown.value;
  updateOutputs();
});
cooldownSeconds.addEventListener("input", () => {
  const value = Number(cooldownSeconds.value);
  if (Number.isFinite(value)) cooldown.value = Math.min(600, Math.max(5, value));
  updateOutputs();
});
document.querySelector("#cancel").addEventListener("click", () => window.desktopPet.closeSettings());
form.addEventListener("submit", async (event) => {
  event.preventDefault();
  await window.desktopPet.saveSettings({
    scale: Number(scale.value),
    speed: Number(speed.value),
    specialIdleCooldownMs: Math.round(Math.min(600, Math.max(5, Number(cooldownSeconds.value))) * 1000),
    openAtLogin: startup.checked,
  });
  window.desktopPet.closeSettings();
});

function updateOutputs() {
  scaleOutput.value = `${Math.round(Number(scale.value) * 100)}%`;
  speedOutput.value = Number(speed.value).toFixed(2);
  cooldownOutput.value = formatDuration(Number(cooldown.value));
}

function formatDuration(seconds) {
  if (seconds >= 60) {
    const minutes = Math.floor(seconds / 60);
    const remainder = seconds % 60;
    return remainder ? `${minutes} 分 ${remainder} 秒` : `${minutes} 分钟`;
  }
  return `${seconds} 秒`;
}
