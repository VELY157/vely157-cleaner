"""Arma VELY157-Cleaner-Mac.zip (con la .app) desde la carpeta publicada de VelyCleanerMac.

Uso: python empaquetar-mac.py <carpeta publish-osx-x64> <zip de plantilla> <zip nuevo> <versión>
Toma Info.plist y AppIcon.icns del zip anterior y pone el permiso de ejecución
que macOS necesita (Windows no guarda permisos Unix por sí solo).
"""
import re
import sys
import zipfile
from pathlib import Path

publish, template, out, version = Path(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4]
app = "VELY157 Cleaner.app/Contents/"
executables = {"VELY157 Cleaner", "createdump"}

with zipfile.ZipFile(template) as t:
    plist = t.read(app + "Info.plist").decode("utf-8")
    icon = t.read(app + "Resources/AppIcon.icns")

plist = re.sub(r"(<key>CFBundleShortVersionString</key>\s*<string>)[^<]*", rf"\g<1>{version}", plist)
plist = re.sub(r"(<key>CFBundleVersion</key>\s*<string>)[^<]*", rf"\g<1>{version}", plist)


def add(z, name, data, mode):
    info = zipfile.ZipInfo(name)
    info.external_attr = mode << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    z.writestr(info, data)


with zipfile.ZipFile(out, "w") as z:
    for d in ["VELY157 Cleaner.app/", app, app + "MacOS/", app + "Resources/"]:
        add(z, d, b"", 0o40755)
    add(z, app + "Info.plist", plist.encode("utf-8"), 0o100644)
    add(z, app + "Resources/AppIcon.icns", icon, 0o100644)
    for f in sorted(publish.rglob("*")):
        rel = f.relative_to(publish).as_posix()
        if f.is_dir():
            add(z, app + "MacOS/" + rel + "/", b"", 0o40755)
        else:
            add(z, app + "MacOS/" + rel, f.read_bytes(), 0o100755 if rel in executables else 0o100644)

print(f"{out}: versión {version}")
