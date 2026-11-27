# Cambios

## 1.1.0

### Nuevo
- **Iconos después de las apps.** La tira de carpetas de la barra de tareas se coloca pegada al último botón de apps (localizado con UI Automation) y lo sigue al abrir o cerrar apps. Sección *Ubicación en la barra de tareas* en Configuración, con un esquema de cada opción: después de las apps (por defecto) o junto a la bandeja.
- **Animación de cierre.** Al abrir una app o cerrar la carpeta, el panel hace la animación inversa a la de apertura: los iconos se encogen y se desvanecen, el último primero.

### Corregido
- **Iconos de la barra de tareas parpadeando al hacer clic fuera.** Al pulsar la barra, Windows la subía sobre la tira hasta la siguiente revisión (500 ms). Ahora la tira se vuelve a subir cada 40 ms.

## 1.0.1

### Corregido
- **Iconos de la barra de tareas visibles sobre apps en pantalla completa.** La tira de carpetas de la barra de tareas se volvía a colocar encima de todo cada 500 ms, incluso sobre un video, un juego o una presentación a pantalla completa. Ahora DeskLauncher consulta a Windows (`SHQueryUserNotificationState`) y oculta la tira, junto con su panel abierto, mientras haya una app en pantalla completa; reaparece al salir.

## 1.0.0

Primera versión estable: multi idioma (inglés y español), renombrar y eliminar perfiles, endurecimiento de seguridad (ver [SECURITY.md](SECURITY.md)) y capturas nuevas.
