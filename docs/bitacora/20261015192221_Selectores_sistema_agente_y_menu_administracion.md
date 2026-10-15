# Selectores de sistema y agente y menú de administración

Fecha: 2026-10-15 19:22:21 (America/Santiago, UTC-03:00).

## Contexto y decisiones

Se solicitó reemplazar los botones de administración de agentes por un único botón Administrar agentes y añadir dos selectores al chat: sistema y agente. El botón abre un menú con Nuevo agente, Editar agente y Eliminar agente, conservando el editor existente y las restricciones de eliminación de perfiles con historial.

## Cambios realizados

- Selectores Sistema y Agente juntos en la cabecera del chat; la lista de agentes se filtra por el sistema seleccionado.
- Los sistemas vacíos mantienen disponible el selector de sistema y la creación de agentes, sin selección de agente ni posibilidad de enviar mensajes. Editar y eliminar requieren un agente seleccionado.
- Los nuevos agentes reciben inicialmente el sistema seleccionado en el chat; el editor permite cambiarlo.
- El cambio de sistema conserva el texto del borrador y solicita confirmación antes de quitar adjuntos. La cancelación restaura la selección anterior. La selección de otro agente conserva las comprobaciones existentes de adjuntos.
- Al cargar una conversación guardada se restauran el sistema y agente correspondientes y se bloquean ambos selectores. Nueva conversación vuelve a habilitarlos. Los refrescos de perfiles mantienen sincronizadas ambas listas.
- Tema pasa a la fila de administración, junto a Administrar agentes y Administrar grupos, dejando espacio para los dos selectores.
- Distribución porcentual explícita en los contenedores del chat y el compositor para evitar desbordamientos al redimensionar o escalar.
- Refuerzo del color de texto de botones habilitados al cambiar de tema: se dibuja con el color de la paleta, evitando texto con un color nativo anterior de contraste insuficiente.
- README actualizado con la navegación, el menú y el comportamiento de los selectores.

## Archivos afectados

- agentest/Forms/ChatForm.cs.
- agentest/Forms/Theming/ThemeButton.cs.
- scripts/Verify.ps1.
- README.md.

## Verificaciones y resultados

- dotnet build agentest.slnx: compilación correcta, cero advertencias y cero errores en esta tarea.
- powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1 -ScreenshotDirectory agentest/bin/verification-screenshots: 135 comprobaciones correctas en la ejecución final.
- Se verificaron filtrado por sistema, sistemas vacíos, conservación del texto del borrador, menú y habilitación de acciones, creación de un agente desde el menú con sistema inicial correcto, actualización de listas, restauración de una conversación de otro sistema y bloqueo y desbloqueo de ambos selectores.
- Capturas revisadas en claro y oscuro, ventana mínima y escalado simulado al 125 %. Se comprobaron los límites de la fila de selectores, el campo del mensaje y los botones del compositor tras escalar. Se corrigieron desbordamientos detectados durante esta revisión.
- Se mantuvieron las verificaciones de grupos, persistencia, contratos, HTTP simulado, monitoreo, cancelación y credenciales de sesión.
- git diff --check: sin errores de espacios, con avisos habituales de normalización LF/CRLF.

## Alcance local y limitaciones

Las pruebas usaron almacenamiento temporal aislado y transporte HTTP simulado. No se llamó a agentes reales ni se modificó la configuración personal de ejecución. Las capturas son locales e ignoradas por Git en agentest/bin/verification-screenshots. El escalado fue simulado, sin probar monitores físicos con diferentes DPI. Las confirmaciones de descarte de adjuntos se conservaron en el flujo; no se automatizó la interacción con esos cuadros de confirmación.