# Riesgos y Observabilidad del Flujo de Asignación

Este documento responde a los puntos 7 y 8 de la actividad: identificar los riesgos operativos y de seguridad, relacionar cada uno con una **mitigación** y una **evidencia de verificación**, y definir las señales que permiten diagnosticar el flujo **sin exponer datos sensibles**.

## 1. Pre-mortem

**Supuesto del ejercicio (video 11):** *"Es mayo de 2027. Durante la temporada alta, el flujo de asignación falló: hubo entregas con dos repartidores, repartidores con más carga de la posible, clientes sin aviso y soporte sin poder explicar qué pasó."*

Trabajando hacia atrás, estas son las causas posibles. Cada una tiene una **señal temprana** que permitiría detectarla antes de que se convierta en un incidente:

| Causa posible | Consecuencia | Señal temprana | Riesgo |
| :--- | :--- | :--- | :--- |
| Dos operadores asignan la misma entrega a la vez | Dos repartidores van por el mismo paquete | Conflictos de concurrencia frecuentes | RO-01 |
| Asignaciones simultáneas al mismo repartidor | Repartidor con más entregas de las que puede llevar | `active_deliveries` cerca o por encima de `max_deliveries` | RO-02 |
| El canal reenvía el mismo pedido | Dos entregas para una compra | Más de una entrega por `order_id` | RO-03 |
| La fuente de disponibilidad se cae | Nadie puede asignar | Aumento de `availability_service_unavailable` | RO-04 |
| El proveedor de notificaciones falla | Cliente y repartidor sin aviso | Crecimiento de `logitracker.outbox.dead_lettered` | RO-05 |
| La aplicación cae entre guardar y avisar | Asignación sin aviso | Mensajes antiguos en estado `Processing` | RO-06 |
| Una clave de operador se filtra | Asignaciones no autorizadas | Asignaciones fuera de horario de un operador | RS-01 |

## 2. Cinco porqués: "una entrega quedó con dos repartidores"

| # | Pregunta | Respuesta | Qué hace el sistema para evitarlo |
| :---: | :--- | :--- | :--- |
| 1 | ¿Por qué quedó con dos repartidores? | Dos asignaciones se guardaron sobre la misma entrega. | — |
| 2 | ¿Por qué se guardaron las dos? | Ambas leyeron la entrega como *pendiente* antes de que la otra terminara. | El dominio solo ve su propia copia; hace falta una protección al guardar. |
| 3 | ¿Por qué el guardado no lo detectó? | El guardado no comprobaba si la entrega había cambiado desde que se leyó. | **Control optimista por versión:** el `UPDATE` incluye `WHERE version = <leída>`. |
| 4 | ¿Y si el problema fuera un reintento del mismo operador? | El operador repite la solicitud porque no recibió respuesta. | **Reintento idempotente:** mismo repartidor → éxito sin cambios (INV-10). |
| 5 | ¿Por qué no se detectó antes de producción? | Las pruebas no simulaban solicitudes simultáneas. | **Prueba de integración concurrente** contra PostgreSQL real: `ESC_08_concurrent_assignments_of_the_same_delivery_leave_exactly_one_courier`. |

La cadena termina en controles verificables, no en culpar a una persona (video 11).

## 3. Registro de riesgos

Todos los nombres de prueba corresponden a pruebas automatizadas que se ejecutan con `scripts/verify.ps1` ([evidencia](evidencia/ejecucion-pruebas.txt)).

### Riesgos operativos

| ID | Riesgo | Mitigación implementada | Evidencia de verificación | Riesgo residual |
| :--- | :--- | :--- | :--- | :--- |
| **RO-01** | Doble asignación de una entrega por operaciones simultáneas | Control optimista por versión; el perdedor recibe `409 concurrency_conflict` o `delivery_already_assigned` | `ESC_08_concurrent_assignments_of_the_same_delivery_leave_exactly_one_courier` (integración) · `ESC_08_translates_a_concurrency_conflict_into_a_business_code` (unitaria) | Bajo |
| **RO-02** | Repartidor por encima de su capacidad por asignaciones simultáneas de entregas distintas | Actualización condicional atómica de la carga + restricción `CHECK` en la tabla (ADR-003 A-02) | `INV_04_concurrent_assignments_of_different_deliveries_never_exceed_courier_capacity` | Bajo |
| **RO-03** | Pedido duplicado por reenvío del canal (R2 de la Actividad 1) | Creación idempotente por pedido + índice único en `order_id` | `R2_the_same_order_sent_twice_creates_a_single_delivery` · `Creating_the_same_order_twice_returns_the_existing_delivery` | Bajo |
| **RO-04** | Fuente de disponibilidad caída o lenta | Tiempo máximo de 3 s; respuesta `503` con `Retry-After`; la entrega no cambia | `ESC_09_availability_outage_leaves_the_delivery_pending_and_reports_a_temporary_error` · demostración con `/api/dev/faults` | Medio: sin disponibilidad no se puede asignar (es el comportamiento correcto) |
| **RO-05** | Proveedor de notificaciones caído | Bandeja de salida; reintentos limitados con espera creciente (2 s, 4 s, 8 s… hasta 5 min); DLQ tras 5 intentos o ante error permanente; reprocesamiento manual | `Notification_outage_keeps_the_assignment_and_moves_the_notice_to_dead_letters_after_retries` · `DispatchOutboxUseCaseTests` (5 pruebas) | Bajo |
| **RO-06** | Aviso perdido si la aplicación cae entre guardar y enviar | La entrega y el aviso se confirman en **la misma transacción**; un mensaje tomado por una instancia caída se libera al vencer su *lease* (30 s) | `ESC_01_assigns_a_courier_end_to_end_and_records_everything_in_one_transaction` | Bajo: entrega **al menos una vez**; el consumidor es idempotente por id de mensaje |
| **RO-07** | Reintento del operador por falla de red duplica efectos | Asignación idempotente con el mismo repartidor (INV-10) | `ESC_03_retrying_the_same_assignment_does_not_duplicate_load_or_notifications` | Bajo |
| **RO-08** | Esquema de base de datos distinto entre entornos | Migraciones versionadas; las pruebas las aplican sobre una base vacía | `Migrations_build_the_schema_from_an_empty_database` | Bajo |
| **RO-09** | El contador de carga se desincroniza de las entregas reales | Solo se modifica dentro de la transacción de asignación y liberación | — | **Medio:** las entregas *Entregada* y *Fallida* aún no descuentan carga (fuera de alcance). Ver próximos pasos. |

### Riesgos de seguridad

| ID | Riesgo | Mitigación implementada | Evidencia de verificación | Riesgo residual |
| :--- | :--- | :--- | :--- | :--- |
| **RS-01** | Acceso sin autenticación o robo de claves desde la base | Clave de API obligatoria; solo se guarda su hash SHA-256; la clave nunca se registra | `RS_01_requests_without_a_valid_api_key_are_rejected` (2 casos) · `RS_01_api_keys_are_stored_only_as_hashes` | Medio: sin rotación ni expiración de claves |
| **RS-02** | Un operador asigna "en nombre de" otro | El operador sale de la identidad autenticada; el contrato HTTP no acepta `operatorId` | `ESC_01_*` comprueba que `assignedBy` es el operador de la clave | Bajo |
| **RS-03** | Datos personales en registros o en mensajes a sistemas externos | Registros con identificadores y códigos únicamente; la bandeja no lleva dirección ni el motivo de liberación (ADR-003 A-10) | `RS_03_outbox_payload_carries_identifiers_only_and_no_personal_data` · plantillas de `ApplicationLog` e `InfrastructureLog` | Bajo |
| **RS-04** | Dependencias con vulnerabilidades conocidas | Auditoría de paquetes en cada compilación (advertencias = errores) + `dotnet list package --vulnerable` | **Hallazgo real:** `Microsoft.OpenApi 2.0.0` (GHSA-v5pm-xwqc-g5wc, alta) bloqueó la compilación; se fijó la 2.13.0. Evidencia: [paquetes-vulnerables.txt](evidencia/paquetes-vulnerables.txt) | Bajo |
| **RS-05** | Fuerza bruta de claves o abuso de la API | Límite de 300 solicitudes por minuto por IP (`429`) | Configuración en `Program.cs` | Medio: sin bloqueo por clave tras intentos fallidos |
| **RS-06** | Errores que revelan detalles internos | Respuestas *ProblemDetails* sin traza de pila; solo `code` y `correlationId` | Error 500 real observado durante el desarrollo sin filtrar detalles · todas las pruebas de error verifican `correlationId` | Bajo |
| **RS-07** | Endpoints de fallas simuladas activos en producción | Solo se publican si `FaultInjection:Enabled` es `true` (apagado por defecto; activo solo en `appsettings.Development.json`) y exigen autenticación | Las pruebas de integración corren con la opción apagada | Bajo |
| **RS-08** | Secretos en el repositorio | Las credenciales del repositorio son **solo de desarrollo local** (`docker-compose.yml`, `appsettings.Development.json`, claves de demostración); en otros entornos se inyectan por variables de entorno | Revisión manual | Medio: requiere disciplina al desplegar |
| **RS-09** | Inyección de texto en los registros mediante la cabecera de correlación | Solo se acepta un identificador alfanumérico de 8 a 64 caracteres | `CorrelationIdMiddleware` · `Health_endpoint_reports_the_database_and_echoes_a_supplied_correlation_id` | Bajo |

**Sobre SAST, DAST y pentesting (video 26):** se aplicó un **SAST básico**: analizadores de .NET con advertencias como errores, y revisión de dependencias. **No** se ejecutó DAST ni pentesting; quedan como próximo paso.

## 4. Señales de observabilidad

### 4.1 Registros estructurados

Cada registro incluye el `CorrelationId` de la solicitud, y en errores este se devuelve al cliente. Soporte puede pedírselo al operador y encontrar todos los registros del caso.

| Evento | Id | Nivel | Contenido |
| :--- | :---: | :--- | :--- |
| Repartidor asignado | 1001 | Information | `DeliveryId`, `CourierId`, `OperatorId` |
| Asignación ya existente (reintento) | 1002 | Information | `DeliveryId`, `CourierId` |
| Asignación rechazada | 1003 | Warning | `DeliveryId`, `CourierId`, `ErrorCode` |
| Fuente de disponibilidad caída | 1004 | Error | `DeliveryId` + excepción |
| Repartidor liberado / liberación rechazada | 1005 / 1006 | Info / Warning | `DeliveryId`, `OperatorId` o `ErrorCode` |
| Entrega creada / pedido repetido | 1007 / 1008 | Information | `DeliveryId`, `OrderId` |
| Aviso enviado | 2001 | Information | `MessageId`, `EventType` |
| Reintento programado | 2002 | Warning | `MessageId`, `Attempt`, `NextAttemptAt` |
| Aviso apartado a la DLQ | 2003 | Error | `MessageId`, `Attempt` + causa |

En desarrollo se muestran en una línea legible; en otros entornos se emiten en **JSON** para un agregador de registros.

### 4.2 Métricas (`Meter` "LogiTracker.Deliveries")

| Métrica | Tipo | Etiquetas | Para qué sirve |
| :--- | :--- | :--- | :--- |
| `logitracker.assignments` | Contador | `outcome`, `error_code` | Tasa de éxito y causas de rechazo. Un aumento de `courier_capacity_reached` indica falta de repartidores. |
| `logitracker.assignment.duration` | Histograma (ms) | `outcome` | p95 del caso de uso. |
| `logitracker.outbox.dispatched` | Contador | `event_type` | Avisos entregados. |
| `logitracker.outbox.failed_attempts` | Contador | `event_type` | Fallas temporales del proveedor. |
| `logitracker.outbox.dead_lettered` | Contador | `event_type` | **Debe ser 0.** Cualquier valor requiere revisión. |

También se registran las métricas HTTP estándar de ASP.NET Core (duración y códigos de respuesta).

### 4.3 Trazas (`ActivitySource` "LogiTracker.Deliveries")

Cada solicitud HTTP genera una traza con tramos para `AssignCourier`, `ReleaseCourier`, `CreateDelivery` y `DispatchOutboxMessage`, etiquetados con `delivery.id`, `courier.id`, `assignment.outcome` y `assignment.error_code`. Con un colector se vería, por ejemplo, si el tiempo se va en la consulta de disponibilidad o en el guardado.

### 4.4 Salud y operación

| Señal | Dónde |
| :--- | :--- |
| Salud de la API y de la base de datos | `GET /health` |
| Avisos apartados que requieren acción | `GET /api/outbox/dead-letters` |
| Carga actual de cada repartidor | `GET /api/couriers` |

### 4.5 Cómo ver las señales

- **Registros:** en la consola de `dotnet run`.
- **Métricas en vivo:** `dotnet tool install -g dotnet-counters` y luego `dotnet-counters monitor --name LogiTracker.Api --counters LogiTracker.Deliveries`.
- **Trazas y métricas exportadas:** arrancar con `Observability__ConsoleExporter=true` para que OpenTelemetry las escriba en la consola. En producción se apuntaría a un colector OTLP.

### 4.6 Información sensible excluida

| Dato | ¿Se registra o se publica? | Motivo |
| :--- | :---: | :--- |
| Dirección de entrega | No | Dato personal del cliente |
| Motivo de liberación | No (solo en el historial de la entrega) | Texto libre que podría contener datos personales |
| Clave de API | Nunca (ni en registros ni en la base) | Credencial |
| Cuerpo de las solicitudes | No | Puede contener datos personales |
| Identificadores (entrega, pedido, repartidor, operador) | Sí | Necesarios para investigar y no identifican a una persona por sí solos |

## 5. Guía rápida de diagnóstico

| Síntoma reportado | Qué revisar | Causa probable |
| :--- | :--- | :--- |
| "No me deja asignar" | `code` de la respuesta y registro 1003 con el `correlationId` | Repartidor saturado, fuera de turno o entrega ya asignada |
| "Error temporal al asignar" | Registro 1004 y `/health` | Fuente de disponibilidad o base de datos caída |
| "El cliente no recibió el aviso" | `/api/outbox/dead-letters` y registros 2002/2003 | Proveedor de notificaciones caído o rechazando |
| "Dos operadores asignaron a la vez" | Registros 1001 y 1003 con `concurrency_conflict` | Comportamiento esperado: solo una asignación gana |
