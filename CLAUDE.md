# VELY157 Cleaner — instrucciones para Claude Code (Mac)

Hola. Este zip es un proyecto real en producción. Léelo completo antes de tocar nada.
Lo escribió otra sesión de Claude Code en la PC Windows del dueño (VELY157 / Vely Media LLC).
El idioma del usuario es **español**: respóndele siempre en español, claro y sin tecnicismos.

## Qué es esto

Un limpiador de PC **de pago**. El cliente solo puede usarlo si mete una **key con
vencimiento** (7, 15, 30… días) que el dueño genera desde un panel. Así cobra por el uso.
Hay tres versiones del limpiador (Windows, Mac, Android) y **un solo servidor de licencias**.

- **Servidor de licencias**: corre en la PC **Windows** del dueño y se publica en
  `https://api.velymediallc.com` a través de un túnel de Cloudflare. Guarda las keys,
  las ata a un equipo, las firma y permite cancelarlas/renovarlas desde un panel web.
- **Las apps** (Windows/Mac/Android) piden la key, la validan contra ese servidor,
  guardan una autorización **firmada** y funcionan hasta 3 días sin internet.

## Por qué te lo mandan a la Mac

La PC Windows **no puede compilar ni probar bien la app de Mac**. Tu trabajo aquí es,
casi seguro, **construir, correr, probar y pulir la app de Mac** (`VelyCleanerMac/`,
hecha con Avalonia) en macOS de verdad, y empaquetarla como `.app`.
**Confírmalo con el usuario antes de grandes cambios.** Puede que solo quiera
compilar/probar, o también retocar el diseño.

## Mapa de carpetas

- `VelyCleanerMac/` — **app de Mac (Avalonia, .NET 8). Lo que probablemente trabajarás.**
  - `Program.cs`, `MainWindow.cs` (toda la interfaz), `Core/License.cs`,
    `Core/MacCleaner.cs` (qué limpia en Mac), `Core/Updates.cs`.
- `VelyLicense/` — **servidor de licencias (ASP.NET Core 8). Multiplataforma**: compila y
  corre en Mac/Linux, pero **en producción vive en la PC Windows del dueño**. Aquí solo
  para leerlo/entenderlo. No montes otro servidor salvo que el usuario lo pida.
- `VelyCleaner/` — app de **Windows** (WinForms). **No se compila en Mac** (WinForms es solo
  Windows). Solo referencia.
- `VelyInstaller/` — instalador azul de **Windows** (WinForms). Tampoco compila en Mac.
- `Shared/Ui.cs` — tema visual azul de las apps **Windows** (WinForms). La de Mac usa Avalonia aparte.
- `velymediallc-web/index.html` — la página `velymediallc.com` ("Muy pronto" + descargas/WhatsApp).
- `marketing/` — imágenes (logo, mockups).
- `mac-release/` — `empaquetar-mac.py` (empaqueta la .app desde Windows), `construir-app.sh`
  (empaqueta la .app **en macOS**, úsalo tú), `app-template/` (Info.plist + AppIcon.icns),
  `LEEME-Mac.txt` (instrucciones para el cliente final).
- `*.ps1 / *.cmd / *.bat` en la raíz — scripts de Windows (instalar el servicio, publicar
  actualizaciones, etc.). Son del lado Windows; no corren en Mac.

> **No incluido en el zip** (para que pese poco): carpetas `bin/`, `obj/`, `publish*/`,
> `payload/`, los `.zip`/`.apk` ya compilados y el código de Android. Se regeneran al compilar.

## Cómo construir y correr la app de Mac

Requisitos en la Mac: **.NET 8 SDK** (`brew install --cask dotnet-sdk`, o desde
dotnet.microsoft.com). Macs con chip Apple (M1/M2/M3): usa `osx-arm64`.

```bash
cd VelyCleanerMac
dotnet run                     # correr y probar la ventana
```

Empaquetar la `.app` (ya con firma ad-hoc, sin cuenta de desarrollador):

```bash
cd mac-release
./construir-app.sh arm64       # o: ./construir-app.sh x64
```

Queda `mac-release/VELY157 Cleaner.app`. Para distribuir, comprímela en .zip.
Nota: sin cuenta de Apple Developer ($99/año), al abrirla macOS pide clic derecho → Abrir
(está explicado en `LEEME-Mac.txt`). La firma/notarización real es una mejora pendiente.

## Cómo funciona la licencia (igual en las 3 apps)

- La app calcula un **ID del equipo** (en Mac, `IOPlatformUUID`) y lo manda con la key a
  `POST https://api.velymediallc.com/api/v1/check`.
- El servidor responde con un **token firmado (ECDSA P-256)**. La app verifica la firma con
  la **clave pública** incrustada (`Core/License.cs → PublicKeyPem`) y la guarda.
- 1 key = 1 equipo. Los días cuentan desde la **activación**. Gracia de 3 días sin internet.
- Prueba gratis: `POST /api/v1/trial` (una por equipo).
- Actualizaciones: `GET /api/v1/version` (versión + sha256 **firmados**) y `GET /download/...`.

## SEGURIDAD — léelo

- La **clave privada** que firma las licencias y la **base de datos** de keys **NO están en
  este zip** (viven solo en la PC Windows, en `C:\ProgramData\VelyLicense`).
  **Nunca** las pidas, las generes aquí ni las metas al proyecto.
- La **clave pública** que ves en `License.cs`/`UpdateService.cs` **sí** va en el código y es
  segura (solo sirve para verificar firmas, no para crearlas). **No la cambies**: tiene que ser
  idéntica a la del servidor o las licencias dejan de validar.
- No subas secretos a ningún lado. Si creas un repo, añade un `.gitignore` para `bin/`, `obj/`,
  `publish*/`, `*.zip`, `*.apk`, `*.pem`, `*.db`.

## Qué NO hacer en la Mac

- No intentes montar IIS, el túnel de Cloudflare, el servicio de Windows ni tocar el registro:
  eso es del lado Windows y ya está funcionando en la PC del dueño.
- No compiles `VelyCleaner/` ni `VelyInstaller/` (son WinForms, solo Windows).
- No cambies la clave pública ni la URL `api.velymediallc.com` sin que el usuario lo pida.

## Estado actual (al momento de este traspaso)

- Windows: limpiador + instalador azul + auto-actualización = **terminados y en producción**.
- Servidor de licencias + panel + página web = **funcionando** en `api.velymediallc.com`.
- Mac: app Avalonia **hecha y YA empaquetada** (el dueño ya tiene el instalable `.app`/.zip de
  Mac; **no está en este zip**). Aquí viene el **código fuente** para seguir mejorándola o
  cambiándola; si recompilas, usa `mac-release/construir-app.sh`. Android: hay un APK compilado
  (su código no viene en este zip).

Pregúntale al usuario qué quiere lograr hoy en la Mac antes de empezar. Suerte.
