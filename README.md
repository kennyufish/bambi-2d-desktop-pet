# Your Cat Desktop Pet

The active product has two maintained parts:

- `desktop-2d/`: Electron Windows desktop pet using transparent 2D sprite packs.
- `app/`, `worker/`, and `db/`: website and cloud-service source.

The former Unity/3D prototype has been removed. Git history retains it if an
older implementation ever needs to be inspected.

## Desktop client

```powershell
cd desktop-2d
npm test
npm start
npm run pack:cache
```

The unpacked executable is written to
`desktop-2d/dist/win-unpacked/YourCatDesktopPet.exe`.

## Website

Requires Node.js `>=22.13.0`.

```powershell
npm install
npm test
```

Use `npm run dev` for local development and `npm run build` for a production
build. Site hosting configuration is stored in `.openai/hosting.json`.
