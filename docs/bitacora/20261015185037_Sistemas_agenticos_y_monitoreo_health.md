# Sistemas agénticos y monitoreo de health

Fecha: 2026-10-15 18:50:37 (America/Santiago, UTC-03:00).

## Contexto y decisiones

Se solicitó conservar el chat y agregar una panorámica de agentes con diagrama y monitoreo parametrizable. Cada perfil pertenece a un único sistema agéntico. Los perfiles anteriores o sin sistema usan DEFAULT. La panorámica es de consulta: la configuración sigue en el editor de perfiles existente. Los sistemas se identifican por nombre sin distinguir mayúsculas y existen mientras tengan perfiles.

El diagrama representa pertenencia al sistema. No se infieren llamadas ni dependencias entre agentes. Los estados de health representan disponibilidad según el criterio configurado, no actividad de ejecución.

## Cambios realizados

- Pestañas Chat y Agentes, conservando el borrador y los controles del chat al navegar.
- Vista con resumen general y por sistema, tarjetas de agentes y conexiones de pertenencia; distribución adaptable al ancho y desplazamiento vertical.
- Modelo por perfil con sistema y configuración de health. Migración compatible a DEFAULT y health sin configurar para datos previos, sin inventar endpoints.
- Editor con pestañas Chat y Sistema y health. Al guardar se exige una URL HTTP/HTTPS de health; se configuran método GET/HEAD/POST sin cuerpo, frecuencia, timeout, código HTTP y validación JSON opcional por ruta y valor exacto.
- Autenticación de health independiente del chat. Encabezado y prefijo configurables; credenciales únicamente en memoria. No se siguen redirecciones ni se conservan cookies de health.
- Servicio HTTP asíncrono con cuatro consultas simultáneas como máximo, cancelación, timeout y límite de lectura JSON de 64 KB. Resultados y detalles sanitizados en memoria.
- Monitoreo periódico desde ambas pestañas y actualización manual sin rondas superpuestas. Se descartan resultados de perfiles reemplazados o eliminados mientras se consultaban. Al cerrar se cancela el monitoreo y la liberación de recursos admite llamadas repetidas.
- Ajustes de temas, tamaños mínimos y desplazamiento del editor. Script de verificación guardado con marca UTF-8 para conservar literales españoles en Windows PowerShell.
- Documentación de uso, configuración, persistencia y limitaciones actualizada.

## Archivos afectados

- agentest/Models/AgentProfile.cs y HealthSettings.cs.
- agentest/Services/HealthClient.cs.
- agentest/Persistence/LocalStore.cs.
- agentest/Forms/ChatForm.cs, ProfileEditorForm.cs y AgentsOverview.cs.
- scripts/Verify.ps1 y README.md.

## Verificaciones y resultados

- dotnet build agentest.slnx: correcto, cero advertencias y cero errores.
- powershell -ExecutionPolicy Bypass -File scripts/Verify.ps1 -ScreenshotDirectory agentest/bin/verification-screenshots: 95 comprobaciones correctas en esta tarea.
- Se comprobaron compatibilidad y persistencia de perfiles, conservación de perfiles inválidos, criterios HTTP y JSON, autenticación, límites de respuesta y concurrencia, errores de red, timeout y cancelación con transporte HTTP simulado.
- Se abrieron formularios WinForms con almacenamiento temporal aislado. Se verificaron pestañas, conservación del borrador, guardado del editor, separación de credenciales, monitoreo periódico en Chat, actualización manual y cancelación de una consulta activa al cerrar.
- Se revisaron capturas del chat, panorámica y editor en claro y oscuro, panorámica a tamaño mínimo y con escalado simulado al 125 %, y editor a tamaño mínimo. Se corrigió una barra horizontal innecesaria al reducir la panorámica.
- git diff --check: sin errores de espacios. Git informa normalización habitual LF/CRLF.

## Alcance local y limitaciones

Las capturas quedaron en agentest/bin/verification-screenshots, dentro de salidas locales ignoradas por Git. Usan perfiles y resultados sintéticos, sin datos personales. Las verificaciones no modificaron la configuración personal de ejecución ni realizaron llamadas a agentes reales.

No se implementaron administración desde la panorámica, relaciones de comunicación entre agentes ni historial de métricas. Los intervalos se cuentan desde la última consulta finalizada y pueden demorarse por rondas largas o por el límite de concurrencia. Los perfiles previos permiten conversar sin health, pero deben completarlo al guardarlos en el editor. El escalado fue simulado y no equivale a una prueba con monitores físicos de diferentes DPI.