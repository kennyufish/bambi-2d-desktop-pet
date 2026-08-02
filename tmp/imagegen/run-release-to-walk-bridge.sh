#!/usr/bin/env bash
set -euo pipefail

export PATH="/c/Users/tszki/AppData/Local/hermes/node:$PATH"

prompt="$(< /c/Users/tszki/Documents/DesktopPet/tmp/imagegen/release-to-walk-prompt.txt)"

/c/Users/tszki/Documents/DesktopPet/tools/gpt-image-bridge/skills/gpt-image-bridge/bin/gpt-image-2 \
  "$prompt" \
  /c/Users/tszki/Documents/DesktopPet/tmp/imagegen/release-to-walk-8f-bridge-chroma.png \
  --size 2048x1024 \
  --image /c/Users/tszki/AppData/Local/Temp/codex-clipboard-a81c1e0e-85da-41fb-b841-f7919843fd51.png \
  --image /c/Users/tszki/AppData/Local/Temp/codex-clipboard-0125b538-4c9b-4dbc-89b2-10dc83ef9ae6.png
