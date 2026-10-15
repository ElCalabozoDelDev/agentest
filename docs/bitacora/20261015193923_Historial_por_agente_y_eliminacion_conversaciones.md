# Historial por agente y eliminación de conversaciones

Fecha: 2026-10-15 19:39:23 (America/Santiago, UTC-03:00).

## Contexto y decisiones

Se solicitó corroborar que la lista lateral mostrara únicamente las conversaciones del agente seleccionado, permitir eliminarlas y destacar la conversación activa. Se confirmó que la lista anterior enumeraba todas las conversaciones cargadas, sin filtrar por perfil.

La lista ahora se filtra por AgentProfileId, no por nombre ni sistema. Los selectores permanecen disponibles al consultar conversaciones existentes para permitir navegar entre historiales de agentes. Cambiar de agente abre un chat nuevo sin reasignar ni modificar la conversación anterior. Esta decisión reemplaza el bloqueo de selectores de la implementación previa.

La eliminación solicitada se implementa sobre el historial local. No se supone un contrato de eliminación remota ni se invoca el backend. La confirmación lo explica e informa cuando también se descartará un borrador.

## Cambios realizados

- Filtrado de conversaciones por el ID del perfil seleccionado, ordenadas por actividad reciente, en cambios de agente, sistema, perfil y carga del historial.
- Cabecera lateral con nombre del agente y cantidad de conversaciones.
- Lista con dibujo propio: fondo azul, borde y franja de énfasis, título en negrita, fecha e indicación Activa. La selección sigue visible al perder el foco. Altura de filas adaptada a la fuente y DPI.
- Navegación entre agentes sin trasladar mensajes ni IDs remotos. El borrador nuevo conserva su texto; las confirmaciones previas a quitar adjuntos y descartar borradores en conversaciones con mensajes se mantienen.
- Botón Eliminar conversación habilitado únicamente para una selección válida y sin operaciones en curso.
- Diálogo de confirmación con opción predeterminada Cancelar. Cancelar conserva datos y borrador.
- Eliminación asíncrona en LocalStore con validación del ID local compacto, sin construir rutas con IDs remotos. Se actualiza la lista solo después de eliminar el archivo correctamente; ante fallo se conserva la conversación visible.
- Tras eliminar se abre la conversación más reciente del mismo agente, o un chat vacío del agente actual si no quedan otras. Los demás historiales se conservan.
- README actualizado con el alcance local de eliminación y la navegación por historiales.

## Archivos afectados

- agentest/Forms/ChatForm.cs y DeleteConversationForm.cs.
- agentest/Forms/Theming/ConversationListBox.cs.
- agentest/Persistence/LocalStore.cs.
- scripts/Verify.ps1 y README.md.

## Verificaciones y resultados

- dotnet build agentest.slnx: correcto, cero advertencias y cero errores.
- powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1 -ScreenshotDirectory agentest/bin/verification-screenshots: 151 comprobaciones correctas en esta tarea.
- Se probaron agentes de igual nombre con IDs distintos, aislamiento de listas, orden por actividad, cambio de agente desde una conversación existente y descarte de la vista anterior sin modificar sus mensajes.
- Se abrió y canceló el diálogo con un borrador, verificando que se conservaran tanto el archivo como el texto. Se confirmó la eliminación y se comprobó selección del siguiente historial, eliminación del último historial y chat vacío del mismo agente.
- Se verificó que los archivos y asociaciones de otros agentes permanecieran intactos y que las conversaciones eliminadas no reaparecieran al reiniciar.
- Se comprobaron rechazo de rutas inválidas e IDs remotos y preservación de otro historial ante un fallo de eliminación de archivo.
- Se revisaron capturas con selección sin foco, ventana mínima, temas claro y oscuro, diálogo de confirmación en ambos temas y escalado simulado al 125 %. Capturas locales en agentest/bin/verification-screenshots, ignoradas por Git.
- git diff --check: sin errores de espacios; avisos habituales LF/CRLF.

## Alcance y limitaciones

Se utilizó almacenamiento temporal aislado y datos sintéticos. No se eliminaron conversaciones personales ni se consultó un backend real. El escalado fue simulado y no equivale a una prueba con monitores físicos de diferentes DPI. Las confirmaciones existentes de cambio de agente con adjuntos o borradores usan cuadros de mensaje y no se automatizó su interacción; sí se automatizaron cancelación y aceptación de la nueva confirmación de eliminación, incluida la advertencia de borrador.