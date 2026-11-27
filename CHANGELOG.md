# Cambios

## 1.0.1

### Corregido
- **Iconos de la barra de tareas visibles sobre apps en pantalla completa.** La tira de carpetas de la barra de tareas se volvía a colocar encima de todo cada 500 ms, incluso sobre un video, un juego o una presentación a pantalla completa. Ahora DeskLauncher consulta a Windows (`SHQueryUserNotificationState`) y oculta la tira, junto con su panel abierto, mientras haya una app en pantalla completa; reaparece al salir.

## 1.0.0

Primera versión estable: multi idioma (inglés y español), renombrar y eliminar perfiles, endurecimiento de seguridad (ver [SECURITY.md](SECURITY.md)) y capturas nuevas.
