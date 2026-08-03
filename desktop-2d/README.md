# Your Cat Desktop Pet 2D

This is the active Windows desktop client. It uses transparent 2D action frames
and Electron; no Unity or 3D assets are required.

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
`sit`, `lieDown`, `sleep`, `pet`, `eat`, `pickedUp`, three rest variants, and
grooming. The active orange-tabby pack contains eight frames per action.

Rebuild the active orange-tabby pack from the supplied transparent 4-by-2
sprite sheets with:

```powershell
python ../tools/import_transparent_8frame_pack.py <source-directory> sprite-packs/orange-tabby [variant-source-directory]
```

Double-click the cat's head to pet it. Right-click anywhere on the cat to choose
an action. A left press becomes a pickup only after the pointer moves at least
six pixels. Automatic rest and grooming actions have a five-minute cooldown.
Tray commands control settings, pause, display scale, and exit.
