#!/bin/bash
# Construye "VELY157 Cleaner.app" en macOS a partir del codigo de ../VelyCleanerMac.
# Uso:  ./construir-app.sh [arm64|x64]      (arm64 = Macs con chip Apple; x64 = Intel)
set -e

ARCH="${1:-arm64}"
RID="osx-$ARCH"
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
SRC="$ROOT/VelyCleanerMac"
APP="$HERE/VELY157 Cleaner.app"

echo "==> Publicando ($RID)…"
dotnet publish "$SRC/VelyCleanerMac.csproj" -c Release -r "$RID" --self-contained true \
  -p:DebugType=none -o "$SRC/publish-$RID"

echo "==> Armando la .app…"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$HERE/app-template/Info.plist"   "$APP/Contents/Info.plist"
cp "$HERE/app-template/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
cp -R "$SRC/publish-$RID/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/VELY157 Cleaner"

echo "==> Firma ad-hoc (sin cuenta de desarrollador)…"
codesign --force --deep --sign - "$APP" || echo "(codesign fallo; la app igual abre con clic derecho -> Abrir)"

echo "Listo: $APP"
echo "Para distribuir:  ditto -c -k --keepParent \"$APP\" \"$HERE/VELY157-Cleaner-Mac.zip\""
