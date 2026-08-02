const form = document.querySelector("#settings-form");
const scale = document.querySelector("#scale");
const speed = document.querySelector("#speed");
const startup = document.querySelector("#startup");
const scaleOutput = document.querySelector("#scale-output");
const speedOutput = document.querySelector("#speed-output");

const settings = await window.desktopPet.getSettings();
scale.value = settings.scale;
speed.value = settings.speed;
startup.checked = settings.openAtLogin;
updateOutputs();

scale.addEventListener("input", updateOutputs);
speed.addEventListener("input", updateOutputs);
document.querySelector("#cancel").addEventListener("click", () => window.desktopPet.closeSettings());
form.addEventListener("submit", async (event) => {
  event.preventDefault();
  await window.desktopPet.saveSettings({
    scale: Number(scale.value),
    speed: Number(speed.value),
    openAtLogin: startup.checked,
  });
  window.desktopPet.closeSettings();
});

function updateOutputs() {
  scaleOutput.value = `${Math.round(Number(scale.value) * 100)}%`;
  speedOutput.value = Number(speed.value).toFixed(2);
}
