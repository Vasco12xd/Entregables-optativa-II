# Resultados, Riesgos Residuales y Próximos Pasos

Registro de lo que se comprobó, con qué resultado y qué queda pendiente. La evidencia de ejecución está en [evidencia/](evidencia/) y se regenera con:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/verify.ps1
```

## 1. Resumen de la ejecución

Última ejecución: **30 de octubre de 2026** · [salida completa](evidencia/ejecucion-pruebas.txt)

| Suite | Qué comprueba | Pruebas | Resultado | Tiempo |
| :--- | :--- | :---: | :---: | :---: |
| `LogiTracker.Domain.Tests` | Invariantes y objetos de valor, sin base de datos ni red | 26 | ✅ 26/26 | < 1 s |
| `LogiTracker.Application.Tests` | Casos de uso con adaptadores en memoria; reintentos y DLQ | 23 | ✅ 23/23 | < 1 s |
| `LogiTracker.IntegrationTests` | API + PostgreSQL real (Testcontainers): flujo, concurrencia, seguridad, migraciones | 20 | ✅ 20/20 | ~9 s |
| `LogiTracker.ArchitectureTests` | Fitness functions sobre la dirección de dependencias | 7 | ✅ 7/7 | < 1 s |
| **Total** | | **76** | ✅ **76/76** | |

| Comprobación de calidad | Resultado | Evidencia |
| :--- | :--- | :--- |
| Compilación con analizadores de .NET, advertencias como errores | ✅ 0 advertencias, 0 errores | [compilacion.txt](evidencia/compilacion.txt) |
| Dependencias con vulnerabilidades conocidas | ✅ Ninguna (tras corregir un hallazgo) | [paquetes-vulnerables.txt](evidencia/paquetes-vulnerables.txt) |
| Estabilidad de las pruebas concurrentes | ✅ 4 ejecuciones seguidas sin fallos | Ejecución manual |

## 2. Escenarios de la Actividad 3 → pruebas

Cada escenario Dado-Cuando-Entonces del diseño tiene al menos una prueba automatizada.

| Escenario | Prueba unitaria | Prueba de integración (HTTP + PostgreSQL) |
| :--- | :--- | :--- |
| ESC-01 Asignar a un repartidor disponible | `ESC_01_assigns_available_courier_to_pending_delivery` · `ESC_01_assigns_saves_and_publishes_the_fact` | `ESC_01_assigns_a_courier_end_to_end_and_records_everything_in_one_transaction` |
| ESC-02 Entrega ya asignada a otro | `ESC_02_rejects_assignment_when_delivery_has_another_courier` · `ESC_02_does_not_query_availability_*` | `ESC_02_rejects_a_second_courier_with_409` |
| ESC-03 Reintento con el mismo repartidor | `ESC_03_repeating_the_same_assignment_does_not_duplicate_it` · `ESC_03_a_retry_*` | `ESC_03_retrying_the_same_assignment_does_not_duplicate_load_or_notifications` |
| ESC-04 Repartidor fuera de turno | `ESC_04_rejects_courier_who_is_off_shift` | `ESC_04_rejects_a_courier_off_shift_with_422` |
| ESC-05 Ruta saturada | `ESC_05_rejects_assignment_when_courier_route_is_saturated` (5/5 y 6/5) · `ESC_05_boundary_accepts_*` (4/5) | `ESC_05_rejects_a_saturated_courier_with_422_and_changes_nothing` |
| ESC-06 Entrega inexistente | `ESC_06_reports_an_unknown_delivery` | `ESC_06_an_unknown_delivery_returns_404` |
| ESC-07 Entrega en camino | `ESC_07_rejects_assignment_of_a_delivery_already_in_transit` | — (no hay endpoint para pasar a *En camino*) |
| ESC-08 Dos operadores a la vez | `ESC_08_translates_a_concurrency_conflict_into_a_business_code` | `ESC_08_concurrent_assignments_of_the_same_delivery_leave_exactly_one_courier` |
| ESC-09 Disponibilidad caída | `ESC_09_availability_outage_leaves_the_delivery_pending_*` | Demostración manual con `/api/dev/faults` (503 + `Retry-After`) |
| ESC-10 Liberar con motivo | `ESC_10_releasing_with_a_reason_*` · `ESC_10_release_with_reason_*` | `ESC_10_releasing_with_reason_frees_the_courier_and_publishes_the_fact` |
| ESC-11 Liberar sin motivo | `ESC_11_a_release_reason_cannot_be_empty` · `ESC_11_release_without_reason_is_rejected` | `ESC_11_releasing_without_reason_is_rejected_with_400` |
| ESC-12 Ventana invertida | `ESC_12_an_inverted_delivery_window_is_rejected` | `ESC_12_an_inverted_window_is_rejected_with_400` |

**Casos de error o límite comprobados** (lo exige el punto 6): ESC-02, 04, 05, 06, 07, 08, 09, 11 y 12, además del límite exacto de capacidad (4 de 5 se acepta, 5 de 5 se rechaza), la concurrencia entre entregas distintas (`INV_04_*`) y la caída del proveedor de notificaciones hasta la DLQ.

## 3. Fitness functions y métricas de arquitectura

| Regla protegida | Prueba | Resultado |
| :--- | :--- | :---: |
| El dominio no depende de nada fuera de la biblioteca base | `Domain_depends_on_nothing_but_the_base_class_library` | ✅ |
| La aplicación no depende de infraestructura, EF Core, Npgsql ni ASP.NET | `Application_depends_only_on_the_domain_and_never_on_infrastructure_or_frameworks` | ✅ |
| La infraestructura no depende de la capa de entrada | `Infrastructure_does_not_depend_on_the_entry_layer` | ✅ |
| Los casos de uso reciben solo abstracciones por constructor | `Use_cases_receive_only_abstractions_through_their_constructor` | ✅ |
| El estado del dominio no se modifica desde fuera del agregado | `Domain_state_cannot_be_changed_from_outside_the_aggregate` | ✅ |
| Los controladores no usan adaptadores de infraestructura | `Controllers_talk_to_use_cases_and_never_to_infrastructure_adapters` | ✅ |

**La prueba de la prueba:** se agregó a propósito un *setter* público a `Delivery.PackageCount`. La fitness function falló señalando `Delivery.PackageCount` y luego se revirtió el cambio. Las reglas detectan violaciones reales, no solo pasan.

**Línea base de métricas** (video 13), registrada por `Architecture_metrics_baseline` para comparar cuando el sistema crezca:

| Métrica | Valor |
| :--- | :---: |
| Tipos públicos del dominio | 16 |
| Puertos (interfaces) en la capa de aplicación | 8 |
| Casos de uso | 4 |
| Adaptadores que implementan puertos | 9 |
| Referencias externas del dominio | **0** |

## 4. Hallazgos durante la construcción

Errores reales que las comprobaciones detectaron y que se corrigieron antes de entregar:

| Hallazgo | Cómo se detectó | Corrección |
| :--- | :--- | :--- |
| Dependencia transitiva `Microsoft.OpenApi 2.0.0` con vulnerabilidad alta (GHSA-v5pm-xwqc-g5wc) | La compilación falló (NU1903, advertencias como errores) | Versión fijada a 2.13.0 |
| `delivery_id` del historial generado como opcional en la migración | Revisión de la migración antes de aplicarla | `.IsRequired()` en el mapeo y migración regenerada |
| Validación de *records* HTTP mal ubicada (error 500 al crear entregas) | Prueba manual de la API | Atributos movidos a los parámetros del constructor |
| Endpoints mínimos sin conversión de enums a texto | Prueba manual de inyección de fallas | `ConfigureHttpJsonOptions` |
| Nombre de excepción sin el sufijo `Exception` y otras reglas de estilo | Analizadores de .NET | Ajustes registrados en el ADR-003 (A-05) |

## 5. Criterios verificables de la Actividad 2: estado

| Criterio | Estado | Evidencia o comentario |
| :--- | :---: | :--- |
| CV-01 Un módulo por área de negocio | 🟡 Parcial | Solo Entregas está implementado; los demás módulos siguen en diseño. |
| CV-02 Comunicación solo por contratos | ✅ | Puertos de aplicación; contrato C-01 por HTTP. |
| CV-03 Ningún módulo accede a datos de otro | ✅ | Esquemas `deliveries` y `access` separados. |
| CV-04 Reglas sin dependencia de la base de datos | ✅ | `Domain_depends_on_nothing_*`, `Application_depends_only_*` |
| CV-05 Sin dependencias circulares | ✅ | Referencias de proyecto acíclicas (las impone el compilador) + fitness functions. |
| CV-06 Pedido reenviado sin duplicados | ✅ | `R2_the_same_order_sent_twice_creates_a_single_delivery` |
| CV-07 Sin sobreventa de la última unidad | ⏸️ No aplica aún | Inventario no está implementado. La misma técnica (guarda atómica) ya protege la capacidad: `INV_04_*`. |
| CV-08 Cambiar la regla de capacidad toca un solo módulo | 🟡 Parcial | El cambio queda dentro de Entregas, pero en dos lugares: dominio y guarda SQL (ADR-003). |
| CV-09 Reemplazar un proveedor solo cambia su adaptador | ✅ | Los casos de uso funcionan con adaptadores en memoria (`Application.Tests`). |
| CV-10 Falla de pagos no detiene la operación | ⏸️ No aplica aún | Pagos está fuera del flujo. Comprobado el equivalente con notificaciones y disponibilidad. |
| CV-11 Rendimiento de la consulta (p95 < 2 s) | ⏳ Pendiente | La métrica `logitracker.assignment.duration` existe; falta una prueba de carga. |
| CV-12 Cada cambio de estado queda auditado | 🟡 Parcial | Historial de asignaciones y liberaciones; la auditoría transversal no está implementada. |

## 6. Riesgos residuales

| Riesgo | Por qué queda | Impacto | Responsable sugerido |
| :--- | :--- | :---: | :--- |
| Contador de carga desincronizado (RO-09) | Las transiciones *Entregada* y *Fallida* no están implementadas y no descuentan carga | Medio | Equipo de Entregas |
| Regla de capacidad en dos lugares | Precio de cerrar la concurrencia con una guarda SQL (ADR-003) | Medio | Equipo de Entregas |
| Claves de API sin rotación ni expiración (RS-01) | La autenticación es mínima a propósito (ADR-003 T-04) | Medio | Seguridad |
| Sin bloqueo por intentos fallidos de clave (RS-05) | Solo hay límite por IP | Medio | Seguridad |
| Sin pruebas de carga | No hay volúmenes reales; los de la Actividad 2 son supuestos | Medio | Equipo + negocio |
| Avisos entregados al menos una vez | Propio del patrón *outbox*; el consumidor simulado es idempotente, un proveedor real debe serlo también | Bajo | Integraciones |
| Proveedores externos simulados | El caso no ofrece proveedores reales de notificaciones ni de turnos | Bajo | Integraciones |

## 7. Próximos pasos priorizados

| # | Paso | Por qué primero | Relación |
| :---: | :--- | :--- | :--- |
| 1 | Implementar *En camino*, *Entregada* y *Fallida*, descontando la carga del repartidor en la misma transacción | Cierra el riesgo residual de mayor impacto operativo | RO-09, ESC-07 |
| 2 | Prueba de carga del flujo con los volúmenes supuestos (SM-01, SM-02) y medir p95 | Valida los supuestos de la Actividad 2 con evidencia | CV-11, EC-01, EC-02 |
| 3 | Conectar OpenTelemetry a un colector (por ejemplo, Grafana o Jaeger) con alertas sobre `outbox.dead_lettered > 0` | Pasar de señales en consola a operación real | Observabilidad |
| 4 | Integración continua: ejecutar `scripts/verify.ps1` en cada cambio, con un *ruleset* que proteja `main` | Hoy la verificación es manual | Video 4, video 27 |
| 5 | Rotación y expiración de claves, o migración a OIDC si aparece un proveedor de identidad | Reduce el riesgo de seguridad residual | RS-01, ADR-003 T-04 |
| 6 | DAST básico (por ejemplo, OWASP ZAP *baseline*) contra la API en un entorno de pruebas | Complementa el SAST con la aplicación en ejecución | Video 26 |
| 7 | Implementar el módulo Pedidos y publicar C-01 como evento real, en lugar del endpoint que lo simula | Siguiente módulo del monolito modular | ADR-001, CV-01 |
