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

## Conversaciones y almacenamiento

- Usa **Nueva conversación** para iniciar otro chat y **Ctrl+Enter** o **Enviar** para enviar un mensaje.
- Puedes cambiar de agente antes del primer envío. Una conversación con mensajes queda asociada a su perfil; el perfil puede editarse para actualizar la conexión.
- **Cancelar** cancela la espera HTTP local. El backend puede haber procesado la solicitud; revisa su estado antes de reenviar.
- Ante un fallo o cancelación, el borrador se recupera durante la sesión. Los borradores sin enviar no se guardan.
- Perfiles: `%LOCALAPPDATA%\Agentest\profiles.json`.
- Conversaciones: `%LOCALAPPDATA%\Agentest\conversations\<id>.json`.
- **Abrir carpeta local** permite consultar esos archivos. Los mensajes y las respuestas se guardan como JSON legible; evita introducir secretos en la plantilla o la URL.
- La escritura usa un archivo temporal y reemplazo. Un historial inválido se conserva en disco y se informa al cargar. Solicitudes pendientes al reiniciar se marcan como interrumpidas.

## Organización

Un solo proyecto y unidad de despliegue, con responsabilidades separadas mediante carpetas:

- `Models`: perfiles, conversaciones, mensajes y metadatos de adjuntos.
- `Forms`: interfaz y coordinación de acciones del usuario.
- `Services`: contrato JSON, conexión HTTP y preparación de adjuntos.
- `Persistence`: carga y escritura de archivos locales.

Los conectores no dependen de controles WinForms. La interfaz consume sus operaciones asíncronas para mantener la ventana disponible durante las solicitudes.

## Verificación

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

El script crea un ejecutable temporal con el SDK, sin paquetes externos ni un segundo proyecto permanente. Comprueba serialización, rutas de respuesta, adjuntos, persistencia, solicitudes y respuestas con un transporte HTTP simulado en memoria, errores, cancelación, timeout e inicialización de los formularios. Opcionalmente, `-ScreenshotDirectory <carpeta>` genera capturas para revisar el diseño. La conexión al orquestador real debe validarse cuando esté disponible su contrato.
