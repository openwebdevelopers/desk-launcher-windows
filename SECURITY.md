# Seguridad

## Reportar una vulnerabilidad

Abre un [aviso de seguridad privado](https://github.com/openwebdevelopers/desk-launcher-windows/security/advisories/new) en GitHub. No publiques detalles en un Issue hasta que exista un arreglo.

## Modelo de seguridad

- DeskLauncher es una app local: no abre conexiones de red, no tiene telemetría ni actualizaciones automáticas.
- Se ejecuta siempre con los permisos del usuario (`asInvoker`); nunca pide elevación.
- Escribe solo en `%APPDATA%\DeskLauncher\` y, si activas *Iniciar con Windows*, en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Lanza únicamente lo que el usuario agregó (apps, accesos directos o carpetas), con el shell de Windows.
- Los archivos de configuración se tratan como no confiables: los identificadores de perfil solo se aceptan como GUID (sin rutas), y las imágenes que se importan tienen límite de tamaño y de píxeles.

## Verificar las descargas

El ejecutable no está firmado. Compara el SHA-256 del `.zip` con el que GitHub muestra como *Digest* junto a cada archivo en la página de cada [release](https://github.com/openwebdevelopers/desk-launcher-windows/releases):

```powershell
Get-FileHash .\DeskLauncher-1.0.1-win-x64.zip -Algorithm SHA256
```
