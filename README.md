# Agentest

Frontend WinForms para conversar con agentes expuestos por una API de orquestación propia. Requiere Windows y el SDK de .NET 10. Un único proyecto, sin paquetes externos.

## Ejecutar

```powershell
dotnet build agentest.slnx
dotnet run --project agentest/agentest.csproj
```

## Configurar un agente

El perfil inicial incorpora el contrato del orquestador facilitado: POST con `query` y `conversation_id`, y respuesta con `answer` y `conversation_id`. Su endpoint `http://localhost:8000/chat` es un **ejemplo**, no una integración verificada. Edita la URL y la autenticación para conectar con el backend real. Puedes crear múltiples perfiles.

Request inicial:

```json
{
  "query": "{{message}}",
  "conversation_id": "{{conversationId}}"
}
```

Respuesta esperada:

```json
{
  "request_id": "string",
  "conversation_id": "string",
  "status": "string",
  "answer": "string"
}
```

`request_id` y `status` se conservan en la respuesta original. Por ahora, cada POST devuelve la respuesta completa; no se implementa polling de tareas pendientes.

Cada perfil contiene nombre, endpoint HTTP/HTTPS, método POST/PUT/PATCH, encabezado y prefijo de autenticación, timeout, plantilla JSON, ruta de respuesta y capacidades de adjuntos. La credencial se ingresa en el editor y se mantiene únicamente en memoria hasta cerrar la app. Para una API key, por ejemplo, usa encabezado `X-Api-Key` y prefijo vacío. Para un token bearer usa `Authorization` y `Bearer`.

### Request

La plantilla debe ser un objeto JSON válido. Los marcadores ocupan **valores JSON completos**, incluyendo las comillas; no se interpolan dentro de otras cadenas. El reemplazo usa serialización JSON, por lo que comillas y saltos de línea del mensaje se conservan correctamente.

```json
{
  "input": "{{message}}",
  "session_id": "{{conversationId}}",
  "context": "{{history}}",
  "files": "{{attachments}}",
  "parameters": { "temperature": 0.2 }
}
```

| Marcador | Valor generado |
| --- | --- |
| `{{message}}` | Texto del mensaje actual |
| `{{conversationId}}` | ID devuelto por el backend; en el primer mensaje se usa el identificador local |
| `{{agentId}}` | Identificador local del perfil |
| `{{history}}` | Array de mensajes anteriores completados: `{"role":"user/assistant","content":"..."}` |
| `{{attachments}}` | Array de adjuntos del mensaje actual |

Los mensajes fallidos, cancelados, pendientes y los avisos del sistema se excluyen del contexto. El mensaje actual se entrega por separado. La plantilla inicial no envía historial ni adjuntos porque no forman parte del contrato facilitado; puedes incluir sus marcadores si otro agente los acepta. Los parámetros fijos pueden tener cualquier estructura JSON. El historial y el formato de adjuntos tienen la estructura indicada arriba; contratos que requieran transformar sus elementos o cargas multipart necesitarán un adaptador adicional.

### Respuesta

Configura una ruta separada por puntos, por ejemplo `answer`, `data.answer` o `results.0.text`. Los índices de arrays son numéricos. La ruta debe resolver a una cadena JSON. Los nombres de propiedad que contienen puntos no se admiten en esta sintaxis.

La ruta del ID de conversación se configura por separado (por defecto `conversation_id`). Su valor se guarda y se utiliza en los siguientes requests, manteniendo separado el ID del archivo local. Deja esta ruta vacía si el backend no devuelve un identificador. Para mostrar un cuerpo HTTP no JSON como texto completo, deja vacías ambas rutas.

La aplicación espera la respuesta HTTP completa; no usa streaming. Las respuestas exitosas conservan también el cuerpo original en el archivo local de conversación.

### Adjuntos

Activa la capacidad en el perfil, indica extensiones separadas por comas (`.txt,.pdf`) o `*`, y el límite por archivo. El request debe incluir el valor `"{{attachments}}"`.

```json
{
  "name": "documento.pdf",
  "content_type": "application/pdf",
  "size": 12345,
  "content_base64": "..."
}
```

Los archivos se envían como Base64 dentro del JSON: no se extrae texto de PDF. El límite por archivo es configurable de 1 a 100 MB; cada mensaje admite hasta 20 archivos y 100 MB en total, antes de codificar. Se guardan solo los metadatos de los adjuntos en el historial local; su contenido debe adjuntarse de nuevo después de reiniciar.

## Sistemas agénticos y monitoreo

La ventana tiene dos pestañas: **Chat** conserva las conversaciones y su borrador; **Agentes** ofrece una panorámica de todos los perfiles, agrupados en sistemas agénticos. Cada agente pertenece a un único sistema. Los perfiles anteriores o con sistema vacío se asignan a **DEFAULT** sin cambiar sus identificadores ni historiales.

Usa **Administrar grupos**, disponible en Chat y Agentes, para crear, renombrar o eliminar sistemas agénticos. Los grupos vacíos se conservan y aparecen en la panorámica. Los nombres se comparan sin distinguir mayúsculas. Renombrar un grupo actualiza la asignación de todos sus agentes; eliminarlo solicita confirmación y los traslada a **DEFAULT** al guardar. DEFAULT no se puede renombrar ni eliminar. Los cambios se aplican mediante **Guardar cambios**; **Cancelar** conserva el catálogo anterior.

En Chat, el selector **Sistema** filtra los agentes disponibles en el selector **Agente**. Un sistema vacío deja la lista de agentes sin selección e impide enviar mensajes. **Administrar agentes** reúne las acciones **Nuevo agente**, **Editar agente** y **Eliminar agente** en un menú; las dos últimas requieren un agente seleccionado. Los nuevos agentes toman inicialmente el sistema seleccionado.

Desde ese menú abre el editor y selecciona un grupo del mantenedor. El editor tiene una única vista desplazable con las secciones **Chat** y **Health**, sin pestañas internas. Configura allí el endpoint de health (obligatorio al guardar) junto con las opciones del chat. Los perfiles anteriores siguen disponibles para conversar aunque todavía no tengan health; aparecen como **Sin configurar**.

Cada perfil permite configurar:

- URL HTTP/HTTPS de health, independiente del endpoint del chat.
- Método GET (inicial), HEAD o POST sin cuerpo.
- Intervalo de 1 a 86400 segundos (inicial: 30), contado desde la última consulta finalizada, y timeout de 1 a 300 segundos (inicial: 5).
- Código HTTP esperado (inicial: 200).
- Ruta JSON opcional, como `status` o `checks.0.ready`, y valor esperado (inicial: `ok`). Las cadenas se comparan exactamente, distinguiendo mayúsculas; para otros valores se usa su representación JSON, por ejemplo `true` o `42`. HEAD solo permite validar el código HTTP. Las claves con puntos no se admiten.
- Encabezado y prefijo de autenticación opcionales, independientes del chat. Sin credencial no se envía autenticación. La credencial se mantiene en memoria durante la sesión y debe ingresarse de nuevo al reiniciar.

El monitoreo comienza automáticamente al abrir la aplicación y continúa en ambas pestañas, con hasta cuatro consultas simultáneas. **Actualizar ahora** fuerza una nueva ronda cuando no hay otra en curso. La interfaz permanece disponible durante las consultas; al cerrar se cancelan las pendientes. Los intervalos son aproximados: una ronda larga puede demorar las consultas siguientes. Las respuestas JSON de health se limitan a 64 KB y no se siguen redirecciones HTTP.

La panorámica muestra el número de agentes saludables por sistema, estado individual, latencia, hora y detalle de la última consulta. Distingue **Sin configurar**, **Sin consultar**, **Saludable**, **No saludable**, **Sin conexión**, **Tiempo agotado** y **Error**. Los resultados viven solo en memoria. Health indica disponibilidad según el criterio configurado; no indica si un agente está ejecutando una tarea.

El diagrama usa conexiones de pertenencia entre el sistema y sus agentes; no representa llamadas ni dependencias entre ellos. Las tarjetas se acomodan al ancho disponible y la vista tiene desplazamiento vertical.

Los grupos y perfiles se guardan juntos en `profiles.json`, mediante escritura temporal y reemplazo. El archivo ahora contiene un objeto con listas `Groups` y `Profiles`; la lista de perfiles del formato anterior se lee y migra automáticamente, importando los sistemas existentes. Los cambios de nombre o eliminación y la reasignación de agentes se guardan en una sola operación. No guardes secretos en URLs, plantillas ni valores esperados.

## Uso del chat y archivos locales

- Usa **Nueva conversación** para iniciar otro chat y **Ctrl+Enter** o **Enviar** para enviar un mensaje.
- Puedes cambiar de sistema y de agente antes del primer envío. El texto del borrador se conserva; si hay adjuntos, se solicita confirmación antes de quitarlos. Una conversación con mensajes restaura su sistema y agente y mantiene bloqueados ambos selectores. **Nueva conversación** vuelve a habilitarlos; el perfil puede editarse para actualizar la conexión.
- **Cancelar** cancela la espera HTTP local. El backend puede haber procesado la solicitud; revisa su estado antes de reenviar.
- Ante un fallo o cancelación, el borrador se recupera durante la sesión. Los borradores sin enviar no se guardan.
- Grupos y perfiles: `%LOCALAPPDATA%\Agentest\profiles.json`.
- Apariencia: `%LOCALAPPDATA%\Agentest\settings.json`.
- Conversaciones: `%LOCALAPPDATA%\Agentest\conversations\<id>.json`.
- **Abrir carpeta local** permite consultar esos archivos. Los mensajes y las respuestas se guardan como JSON legible; evita introducir secretos en la plantilla o la URL.
- La escritura usa un archivo temporal y reemplazo. Un historial inválido se conserva en disco y se informa al cargar. Solicitudes pendientes al reiniciar se marcan como interrumpidas.

## Apariencia

El botón **Tema**, debajo de los selectores y junto a los botones de administración, permite elegir **Sistema**, **Claro** u **Oscuro**. Por defecto se usa Sistema. Cada selección se guarda en `settings.json` y se restaura al abrir la aplicación.

Sistema toma el modo de aplicaciones de Windows y actualiza la apariencia al recibir cambios de preferencias. Claro y Oscuro mantienen la elección del usuario. El tema se aplica al chat, los campos, los botones y el editor de perfiles, conservando el borrador. Se usan una paleta propia y las [API de color de WinForms](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.setcolormode?view=windowsdesktop-10.0); el soporte nativo del tema del sistema corresponde a Windows 11 o posterior.

## Perfil para Agente Tesito

Según el OpenAPI local consultado, configura:

- Endpoint: `http://127.0.0.1:8000/api/v1/conversations/runs`.
- Método: `POST`.
- Ruta del texto de respuesta: `response`.
- Ruta de ID de conversación: `conversation_id`.
- Activa **El backend crea el ID (enviar null al inicio)**.
- Desactiva adjuntos: el contrato `RunCreate` no admite esos campos.
- El OpenAPI consultado no declara autenticación; deja la credencial vacía salvo que tu despliegue la exija.

```json
{
  "message": "{{message}}",
  "conversation_id": "{{conversationId}}"
}
```

El primer request envía `conversation_id: null`; el backend crea la conversación. Los siguientes usan el UUID devuelto por la API. Si esta opción está desactivada, se mantiene el comportamiento anterior: se envía el ID local en el primer request. En Tesito, un ID local no registrado puede provocar 404 aunque la ruta sea correcta.

`/openapi.json` es el documento de descripción, no el endpoint del chat. Este contrato difiere del ejemplo inicial `query`/`answer`. Los errores HTTP muestran el detalle devuelto por el backend para distinguir una ruta inexistente de una conversación inexistente.

El esquema de respuesta permite estados `running`, `completed`, `failed` e `interrupted`, y `response` puede ser null. La integración actual muestra una respuesta textual completa; no consulta automáticamente `GET /api/v1/runs/{run_id}` si el backend deja la ejecución en curso.

## Organización

Un solo proyecto y unidad de despliegue, con responsabilidades separadas mediante carpetas:

- `Models`: perfiles, conversaciones, mensajes y metadatos de adjuntos.
- `Forms`: interfaz y coordinación de acciones del usuario.
- `Services`: contrato JSON, conexión HTTP y preparación de adjuntos.
- `Persistence`: carga y escritura de archivos locales.

Los conectores no dependen de controles WinForms. La interfaz consume sus operaciones asíncronas para mantener la ventana disponible durante las solicitudes.

## Verificación

La verificación de la interfaz incluye el filtrado Sistema → Agente, grupos sin agentes, el menú Administrar agentes, la creación en el sistema seleccionado y la restauración y bloqueo de selectores al abrir conversaciones guardadas.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

El script crea un ejecutable temporal con el SDK, sin paquetes externos ni un segundo proyecto permanente. Comprueba serialización, rutas de respuesta, adjuntos, persistencia, solicitudes y respuestas con un transporte HTTP simulado en memoria, errores, cancelación, timeout e inicialización de los formularios. Incluye migración a DEFAULT, configuración y criterios de health, límite de concurrencia, cancelación y conservación del borrador al cambiar de pestaña. También verifica creación y persistencia de grupos vacíos, nombres duplicados, protección de DEFAULT, renombrado y eliminación con reasignación, cancelación del mantenedor y guardado desde la ventana principal conservando el borrador. Opcionalmente, `-ScreenshotDirectory <carpeta>` genera capturas del chat, la panorámica, el editor y el mantenedor en claro y oscuro, con tamaños mínimos y escalado simulado al 125 % de la panorámica y el mantenedor. Los estados de las capturas de la panorámica son datos sintéticos; no representan un backend real. La conexión al orquestador real debe validarse cuando esté disponible su contrato.
