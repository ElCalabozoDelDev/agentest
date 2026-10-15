# Instrucciones para agentes — Agentest

## Repositorio

Agentest es un frontend de escritorio WinForms para consultar agentes de IA mediante APIs de orquestación propias. Presenta una interfaz de chat con perfiles configurables, adjuntos según las capacidades del agente, historial local y temas claro, oscuro y del sistema.

- Plataforma: Windows, C# y .NET 10 (`net10.0-windows`).
- Solución: `agentest.slnx`.
- Proyecto ejecutable: `agentest/agentest.csproj`.
- Inicio: `agentest/Program.cs`.
- Ventana principal: `agentest/Forms/Main/MainForm.cs`.
- Referencia de uso y contratos: `README.md`.

## Arquitectura y organización

Mantener un único proyecto, una sola capa y una unidad de despliegue. Separar responsabilidades mediante carpetas, sin introducir proyectos de capas, bibliotecas propias adicionales ni dependencias externas sin una instrucción del usuario.

| Carpeta | Responsabilidad |
| --- | --- |
| `agentest/Models` | Perfiles, configuración, conversaciones, mensajes y metadatos de adjuntos. |
| `agentest/Forms` | Interfaz WinForms y coordinación de acciones del usuario. |
| `agentest/Controls` | Vistas y controles WinForms compuestos, separados por funcionalidad, sin acceso a archivos ni clientes HTTP. |
| `agentest/Forms/Theming` | Paletas y aplicación de la apariencia a los controles. |
| `agentest/Services` | Construcción del JSON, cliente HTTP y preparación de adjuntos. |
| `agentest/Persistence` | Lectura y escritura de configuración e historial local. |
| `scripts` | Herramientas de verificación del proyecto. |
| `docs/bitacora` | Registro de cambios relevantes y sus validaciones. |

La lógica HTTP, la transformación de contratos y el acceso a archivos deben permanecer fuera de los eventos visuales. Usar operaciones asíncronas para mantener disponible la interfaz. Respetar los cambios existentes del usuario.

Organizar los formularios en `Forms/Main`, `Forms/Agents`, `Forms/Groups` y `Forms/Conversations`; la coordinación del chat está en `Forms/Chat`. Los controles se agrupan en `Controls/Chat`, `Controls/Conversations` y `Controls/Agents`. Separar disposición visual en `.Designer.cs`, comportamiento en `.cs` y recursos en `.resx`. Los controles con dibujo propio pueden definir su representación directamente. Mantener los constructores de vistas libres de acceso a datos y llamadas HTTP; la inicialización de ejecución comienza al mostrar la ventana principal. Consultar `docs/arquitectura.md` para las responsabilidades y el ciclo de vida.

## Forma de trabajo

- Antes de modificar código, entregar una planificación y formular las preguntas necesarias para aclarar el alcance. Aprovechar las respuestas y decisiones ya dadas en el chat; no volver a preguntar lo que ya está resuelto.
- Revisar las instrucciones del repositorio y el código afectado antes de editar. Mantener los cambios dentro del alcance solicitado.
- Mantener la interfaz y la documentación de uso en español.
- Hacer configurables los contratos y capacidades por perfil. No asumir que todos los agentes comparten endpoint, autenticación, adjuntos o estructura JSON.
- Reemplazar marcadores mediante serialización JSON; evitar concatenar texto del usuario para construir payloads.
- Distinguir el ID local del historial del ID remoto de conversación. Si el backend crea la conversación, enviar `null` al inicio y reutilizar el ID de su respuesta.
- Mantener las credenciales en memoria durante la sesión. No agregar secretos, datos de conversaciones del usuario ni configuración personal al repositorio o a la bitácora.
- Conservar la escritura mediante archivo temporal y reemplazo. Evitar sobrescribir historiales inválidos o descartar borradores sin informar al usuario.
- Ante ajustes visuales, revisar tamaños, escalado, contraste y los temas claro y oscuro en los formularios afectados.
- Actualizar `README.md` cuando cambien la configuración, el uso o los contratos admitidos.

## Persistencia y API

Los datos de ejecución se guardan en `%LOCALAPPDATA%\Agentest`, fuera del repositorio:

- `profiles.json`: perfiles de agentes.
- `settings.json`: preferencias de apariencia.
- `conversations/<id>.json`: mensajes y respuestas originales; los adjuntos conservan solo metadatos.

El perfil de ejemplo genérico utiliza `query`/`answer`. La integración documentada con Agente Tesito usa `POST /api/v1/conversations/runs`, request con `message` y `conversation_id`, y texto de respuesta en `response`. No confundir `/openapi.json` con el endpoint de ejecución. Consultar `README.md` para la configuración completa.

Actualmente no se implementan streaming, polling de ejecuciones pendientes ni carga multipart. Los adjuntos admitidos se codifican como Base64 dentro del JSON.

## Validación

Desde la raíz:

```powershell
dotnet build agentest.slnx
dotnet run --project agentest/agentest.csproj
powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1
```

El script utiliza un proyecto temporal, sin paquetes externos, y salidas aisladas para no sobrescribir una instancia abierta de la aplicación. Admite `-ScreenshotDirectory <carpeta>` para revisar los formularios.

Ejecutar las comprobaciones pertinentes al cambio. Para cambios solo de documentación, revisar contenido, enlaces y nombres; no es necesario recompilar la aplicación. Informar qué se verificó realmente y distinguir pruebas simuladas de llamadas a un backend real. No presentar resultados históricos como comprobaciones de la ejecución actual.

## Bitácora obligatoria

Por cada tarea que produzca cambios relevantes en el repositorio, crear una entrada Markdown en `docs/bitacora` antes de darla por terminada. Incluye cambios de funcionalidad, arquitectura, correcciones, configuración del proyecto, verificaciones y documentación relevante.

- Formato obligatorio del nombre: `AAAAMMDDHHMMSS_Titulo.md`.
- Obtener la fecha y hora reales al registrar la entrada, usando la zona `America/Santiago`, salvo indicación distinta del usuario.
- Usar un título breve y descriptivo; preferir letras ASCII y guiones bajos para separar palabras en el nombre del archivo.
- Ejemplo de formato: `20261015182730_Resumen_cambios_del_chat.md`.
- Incluir fecha y zona horaria, contexto, cambios realizados, archivos o carpetas afectados, verificaciones y resultados, y limitaciones o pendientes cuando existan.
- Registrar las decisiones que expliquen el comportamiento final. Evitar narrar cada comando o intento descartado.
- Distinguir modificaciones del repositorio de ajustes locales o acciones externas. No guardar credenciales ni copiar conversaciones privadas.
- Agrupar cambios de una misma tarea en una entrada coherente. No crear otra entrada únicamente para registrar la propia escritura de esa bitácora.
- Conservar las entradas anteriores. Corregirlas si contienen errores; documentar las evoluciones posteriores con nuevas entradas.

El resumen inicial de los cambios realizados en este chat está en `docs/bitacora/20261015182730_Resumen_cambios_del_chat.md`.
