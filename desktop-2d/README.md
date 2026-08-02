# Your Cat Desktop Pet 2D

This is the active Windows desktop client. It uses transparent 2D action frames
instead of Unity or a 3D model. The previous `unity-client` remains only as a
historical prototype.

## Commands

```powershell
# Validate the sprite pack and state/settings logic
npm test

# Start when Electron dependencies are installed
npm start

# Produce the unpacked Windows client from the verified local Electron cache
npm run pack:cache
```

The unpacked application is written to
`dist/win-unpacked/YourCatDesktopPet.exe`. No installer is produced.

## Sprite packs

Each pack contains a `manifest.json`, transparent frames under `frames/`, and
optional source sheets under `source/`. Required actions are `idle`, `walk`,
`sit`, `lieDown`, `sleep`, `pet`, `eat`, and `pickedUp`. The active orange-tabby
pack contains eight frames per action.

Rebuild the active orange-tabby pack from the supplied transparent 4-by-2
sprite sheets with:

```powershell
python ../tools/import_transparent_8frame_pack.py <source-directory> sprite-packs/orange-tabby
```

Right-click the cat to pet it, Shift+right-click to feed it, and hold the left
button to play the picked-up animation while dragging it. Tray commands control
settings, pause, display scale, and exit.
