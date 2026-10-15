# Mantenedor de grupos y editor unificado

Fecha: 2026-10-15 19:03:51 (America/Santiago, UTC-03:00).

## Contexto y decisiones

Se solicitó simplificar la interfaz, agregar un mantenedor de grupos y reunir health con las opciones de chat. Se conservaron las pestañas principales Chat y Agentes; se eliminaron las pestañas internas del editor del perfil. Cada grupo sigue representando un sistema agéntico y cada agente pertenece a uno solo.

DEFAULT es el grupo protegido de respaldo. Un grupo puede existir sin agentes. Renombrarlo actualiza las pertenencias; eliminarlo requiere confirmación y reasigna sus agentes a DEFAULT al guardar. El diálogo trabaja sobre una copia del catálogo, de modo que Cancelar conserva el estado previo.

## Cambios realizados

- Nuevo mantenedor con lista de grupos, cantidad de agentes, creación, renombrado, eliminación y botones Guardar cambios y Cancelar.
- Acceso mediante Administrar grupos desde Chat y desde la panorámica Agentes.
- Selector de grupos existentes en el editor de perfiles. Chat y Health quedan en una única vista desplazable con secciones identificadas y botones de guardado fijos.
- Catálogo persistente con listas Groups y Profiles dentro de profiles.json. La lectura admite la lista antigua y migra los sistemas existentes conservando IDs y opciones de perfiles.
- Grupos, renombrados y reasignaciones se escriben juntos mediante archivo temporal y reemplazo, evitando escrituras separadas que dejen asignaciones inconsistentes. Guardar perfiles conserva los grupos vacíos.
- La panorámica muestra grupos vacíos con una indicación para asignar perfiles desde Chat.
- Validaciones de nombres vacíos, longitud, caracteres de control, duplicados sin distinguir mayúsculas y protección de DEFAULT.
- README actualizado con el flujo de administración, el editor unificado y el nuevo formato persistido.

## Archivos afectados

- agentest/Models/AgentCatalog.cs.
- agentest/Services/AgentGroups.cs.
- agentest/Persistence/LocalStore.cs.
- agentest/Forms/GroupManagerForm.cs, ProfileEditorForm.cs, AgentsOverview.cs y ChatForm.cs.
- scripts/Verify.ps1 y README.md.

## Verificaciones y resultados

- dotnet build agentest.slnx: correcto, cero advertencias y cero errores en esta tarea.
- powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1 -ScreenshotDirectory agentest/bin/verification-screenshots: 118 comprobaciones correctas en la ejecución final.
- Se verificaron migración y persistencia del catálogo, grupos vacíos, duplicados, protección de DEFAULT, renombrado preservando IDs, eliminación con reasignación y conservación de archivos inválidos.
- Se abrió el mantenedor desde la ventana principal y se guardaron un renombrado y un grupo vacío, comprobando persistencia, actualización de la panorámica y conservación del borrador del chat.
- Se verificaron edición y cancelación sobre copias, selección del grupo y guardado de health desde el editor sin pestañas internas. Se conservaron las comprobaciones existentes de HTTP simulado, credenciales en memoria, monitoreo, chat y temas.
- Revisión visual de capturas del mantenedor y editor en claro y oscuro, tamaños mínimos y escalado simulado del mantenedor al 125 %. Las capturas están dentro de agentest/bin/verification-screenshots, ignoradas por Git.
- git diff --check: sin errores de espacios; avisos habituales de normalización LF/CRLF.

## Limitaciones y alcance local

No se realizaron llamadas a agentes reales ni se modificó la configuración personal de ejecución. Los datos de prueba son sintéticos y el almacenamiento de pruebas está aislado. El escalado es una simulación; no sustituye una prueba con monitores físicos de diferentes DPI.

El formato nuevo de profiles.json se lee de forma compatible con la lista anterior, pero una versión antigua de la aplicación que solo admita listas no podrá leer el catálogo nuevo. Las relaciones del diagrama continúan representando pertenencia a sistemas, sin inferir comunicaciones entre agentes.