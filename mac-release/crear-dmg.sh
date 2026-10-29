#!/bin/bash
# Arma VELY157-Cleaner-Mac.dmg: una ventana con la app a la izquierda y un acceso
# directo a Aplicaciones a la derecha (como el instalador de Claude Desktop).
# Uso:  ./crear-dmg.sh [ruta/al/fondo.png]
set -e

HERE="$(cd "$(dirname "$0")" && pwd)"
APP="$HERE/VELY157 Cleaner.app"
VOLNAME="VELY157 Cleaner"
DMG_FINAL="$HERE/VELY157-Cleaner-Mac.dmg"
BG_SRC="${1:-}"
STAGE="$(mktemp -d)/dmg-stage"

if [ ! -d "$APP" ]; then
  echo "No encuentro '$APP'. Corre antes ./construir-app.sh [arm64|x64]."
  exit 1
fi

mkdir -p "$STAGE/.background"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Aplicaciones"
if [ -n "$BG_SRC" ] && [ -f "$BG_SRC" ]; then
  cp "$BG_SRC" "$STAGE/.background/bg.png"
fi

TMP_DMG="$(mktemp -u).dmg"
hdiutil create -volname "$VOLNAME" -srcfolder "$STAGE" -ov -format UDRW "$TMP_DMG" >/dev/null
MOUNT_DIR="$(mktemp -d)"
hdiutil attach "$TMP_DMG" -mountpoint "$MOUNT_DIR" -quiet

# Acomoda ventana, fondo e iconos por AppleScript (necesita permiso de
# Automation sobre Finder la primera vez). Si no hay permiso, seguimos sin
# eso: el .dmg igual sirve, solo que con la vista por defecto de Finder.
if [ -f "$MOUNT_DIR/.background/bg.png" ]; then
  osascript <<OSA || echo "(no pude personalizar la ventana del .dmg; falta permiso de Automation para Finder. El .dmg sigue sirviendo igual.)"
tell application "Finder"
  tell disk "$VOLNAME"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set the bounds of container window to {200, 120, 860, 520}
    set theViewOptions to the icon view options of container window
    set arrangement of theViewOptions to not arranged
    set icon size of theViewOptions to 96
    set background picture of theViewOptions to file ".background:bg.png"
    set position of item "VELY157 Cleaner.app" of container window to {170, 230}
    set position of item "Aplicaciones" of container window to {480, 230}
    update without registering applications
    delay 1
    close
  end tell
end tell
OSA
fi

chmod -Rf go-w "$MOUNT_DIR" 2>/dev/null || true
sync
hdiutil detach "$MOUNT_DIR" -quiet
rm -f "$DMG_FINAL"
hdiutil convert "$TMP_DMG" -format UDZO -imagekey zlib-level=9 -o "$DMG_FINAL" >/dev/null
rm -f "$TMP_DMG"
rm -rf "$(dirname "$STAGE")" "$MOUNT_DIR"

echo "Listo: $DMG_FINAL"
