# Resumen de los cambios realizados en este chat

Fecha de registro: 2026-10-15 18:27:30, America/Santiago (UTC-03:00).

## Contexto y decisiones

El proyecto partió de un formulario WinForms vacío en .NET 10. Se definió un frontend de chat para un backend agéntico propio, con perfiles por agente y contratos JSON configurables.

Se acordó mantener un solo proyecto, una sola capa y ninguna biblioteca adicional, separando responsabilidades mediante carpetas. Se hicieron preguntas y una planificación antes de implementar el frontend. El historial se guarda en una carpeta local y cada perfil determina las capacidades de adjuntos.

## Cambios del repositorio

### Interfaz de chat y organización

- Se sustituyó el formulario inicial `Form1` por `Forms/ChatForm.cs` y se actualizó `Program.cs` para iniciarlo.
- Se agregó un historial visual, entrada multilínea, envío mediante botón o Ctrl+Enter, cancelación, lista de conversaciones y creación de nuevos chats.
- Se incorporaron selección, creación, edición y eliminación de perfiles. Los perfiles asociados a conversaciones guardadas no se eliminan desde la interfaz.
- Se agregó selección y eliminación de adjuntos pendientes, controles adaptados a las capacidades del perfil y acceso a la carpeta local.
- Se separaron los datos, la interfaz, los servicios y la persistencia en `Models`, `Forms`, `Services` y `Persistence`.

### Perfiles y comunicación con el orquestador

- Se implementaron endpoint, método HTTP, encabezado y prefijo de autenticación, tiempo de espera, plantilla de request y rutas para extraer texto e ID de conversación de la respuesta.
- Se agregó envío asíncrono con `HttpClient`, cuerpo JSON UTF-8 y encabezado `Accept: application/json`.
- Se incorporaron los marcadores `{{message}}`, `{{conversationId}}`, `{{agentId}}`, `{{history}}` y `{{attachments}}`. Sus valores se reemplazan mediante serialización JSON, conservando tipos y escapando el texto correctamente.
- El historial enviado como contexto contiene mensajes anteriores completados; excluye el mensaje actual y los mensajes fallidos, cancelados o pendientes.
- Se implementó extracción de respuestas mediante rutas separadas por puntos e índices numéricos de arrays.
- Las credenciales se mantienen en memoria durante la sesión y no se escriben como un campo de credencial del perfil.
- Se incorporaron errores HTTP, timeout, cancelación y recuperación del borrador durante la sesión. Los errores HTTP muestran también el detalle del backend.

### Adjuntos

- Se agregaron capacidades de adjuntos por perfil, extensiones permitidas y límite por archivo.
- Los archivos se envían como objetos JSON con nombre, tipo de contenido, tamaño y Base64. El contrato del agente debe admitirlos explícitamente.
- Se establecieron límites de hasta 20 archivos y 100 MB por mensaje antes de codificar, con un límite por archivo configurable de 1 a 100 MB.
- Se conservan únicamente los metadatos de adjuntos en los historiales locales. No se extrae texto de PDF ni se implementa multipart.

### Persistencia local

- Se implementó `LocalStore` con archivos en `%LOCALAPPDATA%\Agentest`.
- Los perfiles se guardan en `profiles.json`, los temas en `settings.json` y las conversaciones en archivos individuales de `conversations`.
- Se separó el ID local de conversación del ID devuelto por el backend, conservando este último entre mensajes y reinicios.
- Las respuestas exitosas guardan también el cuerpo HTTP original.
- Se implementó escritura mediante archivo temporal y reemplazo, validación de IDs de archivos, aislamiento de historiales inválidos y detección de solicitudes interrumpidas al reiniciar.
- Los borradores no enviados no se guardan entre sesiones; se agregaron avisos antes de descartarlos al cambiar de conversación o cerrar.

### Corrección de botones de perfiles

- Se reemplazó la altura fija de la fila de botones de perfiles por ajuste automático, con margen inferior.
- El cambio evita que el elemento inferior recorte los botones al variar el tamaño de los controles o la escala de pantalla.
- Se aislaron las salidas de compilación del script de verificación para poder verificar sin sobrescribir el ejecutable de una instancia abierta.

### Temas claro, oscuro y del sistema

- Se agregó el botón Tema junto al selector de agente, con opciones Sistema, Claro y Oscuro.
- La elección se persiste localmente y se restaura al abrir la aplicación; el modo Sistema sigue los cambios de preferencias de Windows.
- Se crearon paletas y aplicación de apariencia en `Forms/Theming`, incluyendo el menú, los formularios y los colores de roles del chat.
- Se ajustó el contraste de botones deshabilitados mediante `ThemeButton`.
- El cambio de tema conserva el borrador y aplica la apariencia al editor de perfiles.

### Compatibilidad con Agente Tesito y diagnóstico del 404

- El contrato inicial facilitado usaba `query`/`answer`; se conservó como ejemplo genérico configurable.
- Al revisar el OpenAPI del backend real se identificó `POST /api/v1/conversations/runs`, con `message` y `conversation_id` en el request, y `response` y `conversation_id` en la respuesta.
- Se comprobó que el backend crea una conversación cuando el ID inicial es null, mientras que un ID local no registrado provoca `conversation_not_found`.
- Se agregó `BackendCreatesConversation` al modelo y al editor de perfiles: al activarlo, `{{conversationId}}` produce null hasta recibir el ID remoto. Desactivado, se conserva el comportamiento de generar el ID inicial localmente.
- Se incorporó validación de la ruta del ID de respuesta para los perfiles donde el backend crea la conversación.
- Se documentaron el endpoint, los campos y las limitaciones de esta integración en `README.md`.

### Documentación y herramientas

- Se creó `README.md` con instrucciones de ejecución, organización, contratos, autenticación, almacenamiento, adjuntos, temas y configuración de Tesito.
- Se creó `scripts/Verify.ps1`, que genera y elimina un ejecutable de verificación temporal, sin un segundo proyecto permanente ni paquetes externos.
- En esta tarea se crearon `docs/bitacora` y el presente resumen inicial.
- Se creó `AGENTS.md` en la raíz con información del repositorio, instrucciones de trabajo, arquitectura, validación y registro obligatorio de cambios relevantes en la bitácora.

## Ajuste local fuera del repositorio

Durante el diagnóstico se revisó el perfil local `agente-tesito`. Su endpoint y rutas de respuesta ya eran correctos, pero `BackendCreatesConversation` estaba desactivado y los chats fallidos no tenían ID remoto.

Se activó esa opción en el archivo local de perfiles y se conservó una copia de respaldo. Este ajuste corresponde a datos de ejecución fuera del repositorio; no se incorporaron esos perfiles ni conversaciones a los archivos versionados. Una instancia abierta debe reiniciarse para cargar el archivo modificado externamente.

## Validaciones realizadas durante el chat

- Se realizaron compilaciones y revisiones visuales del chat y del editor de perfiles, incluyendo las variantes clara y oscura.
- La última ejecución de `scripts/Verify.ps1` previa a esta documentación aprobó **57 comprobaciones**. Cubrió contratos JSON, transporte HTTP simulado, errores, timeout, cancelación, persistencia, adjuntos, formularios, temas y la creación/reutilización del ID de conversación para Tesito.
- Se consultó el documento OpenAPI real para verificar rutas y esquemas.
- Después del ajuste local del perfil, se realizó un POST real de prueba con `conversation_id: null`: devolvió **HTTP 201**, estado **completed** y texto **Ok**. Esto verificó el primer envío al backend; no equivale a una prueba completa de todos los flujos de la interfaz.
- Para la tarea actual, que solo agrega documentación, se revisaron los archivos existentes, la estructura y el estado del repositorio. No se volvieron a ejecutar las pruebas funcionales ni la consulta al agente.

## Limitaciones y pendientes

- No hay streaming ni polling de ejecuciones. Tesito permite estados pendientes o fallidos y respuestas null; el frontend actual espera un texto de respuesta completo.
- Los nombres de propiedades con puntos no están admitidos por la sintaxis de rutas de respuesta.
- Los adjuntos usan JSON/Base64 y requieren compatibilidad explícita del backend. El contrato de Tesito consultado no admite adjuntos.
- Los borradores y las credenciales no se restauran entre sesiones. El contenido binario de adjuntos debe volver a seleccionarse después de reiniciar.
- Los perfiles de ejemplo no sustituyen la configuración del endpoint y del contrato de cada agente.
