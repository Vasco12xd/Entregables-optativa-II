# ADR-003: Implementación del vertical slice de asignación y ajustes al diseño

| Campo | Valor |
| :--- | :--- |
| **Estado** | Aceptada |
| **Fecha** | 2026-10-30 |
| **Actividad** | Actividad 4: Construcción, validación y defensa final |
| **Depende de** | [ADR-001](ADR-001-estructura-monolito-modular.md) (monolito modular), [ADR-002](ADR-002-diseno-flujo-asignacion.md) (diseño del flujo) |
| **Documentos relacionados** | [Implementación](../actividad-4/implementacion.md), [Riesgos y observabilidad](../actividad-4/riesgos-y-observabilidad.md), [Resultados](../actividad-4/resultados-y-proximos-pasos.md) |

## Contexto

La Actividad 3 diseñó el flujo **asignar un repartidor a una entrega** sin elegir tecnología. Para construirlo como un *vertical slice* ejecutable hay que decidir con qué se implementa cada capa y resolver los temas que el diseño dejó abiertos:

- La **limitación de capacidad** entre entregas distintas (ADR-002, riesgos).
- El riesgo de **perder un aviso** entre guardar y publicar (ADR-002, D-06).
- La **identidad del operador**, que el diseño recibía en el comando sin decir de dónde salía.

Restricciones: un equipo pequeño (SC-01), sin automatización madura de despliegue (SC-02) y la prioridad de confiabilidad sobre sofisticación (RES-05).

## Decisiones tecnológicas

| ID | Decisión | Alternativas descartadas | Razón | Costo aceptado |
| :--- | :--- | :--- | :--- | :--- |
| **T-01** | **.NET 10 / C#** con ASP.NET Core (controladores) | Node.js, Java/Spring | El material del curso y los fragmentos de la Actividad 3 están en C#; el equipo conoce el ecosistema. | Atado al ecosistema .NET. El dominio no depende de ASP.NET, así que la entrada podría cambiar sin tocar las reglas. |
| **T-02** | **PostgreSQL 17** en Docker (`docker-compose.yml`) | SQLite, SQL Server | Bloqueos por fila, actualizaciones condicionales y `FOR UPDATE SKIP LOCKED`, que la concurrencia y el *outbox* necesitan. Se levanta con un comando. | Requiere Docker para desarrollar y probar. |
| **T-03** | **Migraciones de EF Core**, con `dotnet-ef` como herramienta local del repositorio | Flyway (video 15) | Cumple el mismo principio que Flyway: versiones numeradas, tabla de historial y no editar lo ya aplicado. Además se genera desde el modelo y no exige otro *runtime*. | La migración la genera una herramienta; hay que revisarla. Ya ocurrió: `delivery_id` salió opcional y se corrigió antes de aplicarse. |
| **T-04** | **Clave de API por operador**, guardada como hash SHA-256 | `OperatorId` en el cuerpo · OAuth/OIDC | El operador no puede hacerse pasar por otro (riesgo RS-02). OAuth necesita un proveedor de identidad que el caso no tiene. | No hay rotación ni expiración de claves (riesgo residual). Migrar a OIDC solo cambia el *handler* de autenticación. |
| **T-05** | **Testcontainers**: PostgreSQL real y efímero en las pruebas de integración | Proveedor en memoria de EF Core | Solo una base real prueba SQL, migraciones, bloqueos y concurrencia. | Las pruebas de integración necesitan Docker y tardan unos segundos más. |
| **T-06** | **Observabilidad con las APIs estándar de .NET** (`ActivitySource`, `Meter`, `ILogger`) exportadas con **OpenTelemetry** | SDK de un proveedor concreto | La aplicación no depende de un proveedor de monitoreo (video 28). | Por ahora se exporta a consola; falta un colector real. |
| **T-07** | **Inyección de fallas** solo en desarrollo (decorador y `/api/dev/faults`) | Probar fallas solo con pruebas automáticas | Permite demostrar en vivo, de forma controlada, la respuesta ante una dependencia caída (video 28). | Código adicional que nunca debe activarse en producción: está apagado por configuración y requiere autenticación. |

## Ajustes al diseño de la Actividad 3

El punto 5 del enunciado pide documentar toda decisión de implementación que ajuste el diseño. Cada ajuste queda trazado aquí:

| ID | Diseño (Actividad 3) | Implementación | Motivo |
| :--- | :--- | :--- | :--- |
| **A-01** | Paso 5 guardar, paso 6 publicar. | El hecho se **registra en la bandeja de salida** (paso 5) y se **confirma junto con la entrega** en una sola transacción (paso 6). El envío real lo hace el despachador. | Cierra el riesgo de ADR-002 D-06: ya no puede guardarse la asignación y perderse el aviso. |
| **A-02** | Limitación aceptada: dos entregas distintas podían superar la capacidad del repartidor. | **Guarda atómica** en el repositorio: `UPDATE couriers SET active_deliveries = active_deliveries + 1 WHERE ... AND active_deliveries < max_deliveries`, dentro de la misma transacción. Además, una restricción `CHECK` en la tabla. | Es la opción (a) que propuso el ADR-002. PostgreSQL serializa la fila del repartidor, así que la segunda asignación no encuentra cupo. Prueba: `INV_04_concurrent_assignments_of_different_deliveries_never_exceed_courier_capacity`. |
| **A-03** | `CourierAssignment` como objeto de valor. | Entidad hija del agregado, con **identificador técnico**. | El historial necesita persistirse como filas. Sus datos no cambian salvo al liberarse, y solo la entrega puede crearla o liberarla. |
| **A-04** | `AssignCourierCommand` recibe `OperatorId`. | El comando lo conserva, pero el **contrato HTTP no lo incluye**: el controlador lo toma de la identidad autenticada. | Evita la suplantación (RS-02). |
| **A-05** | Excepción `DomainRuleViolation`. | `DomainRuleViolationException`. | Convención de .NET, exigida por el analizador CA1710. |
| **A-06** | Adaptadores `SqlDeliveryRepository` y `CourierRosterAvailabilityAdapter`. | `EfDeliveryRepository` y `EfCourierAvailability`. | Nombres según la tecnología real. Los puertos no cambiaron. |
| **A-07** | Control de versión sin ubicación definida. | Propiedad **oculta** `Version` en la configuración de EF Core, fuera del dominio. | La concurrencia es un detalle de persistencia; el dominio no la necesita. |
| **A-08** | Reintento idempotente resuelto en la entidad. | La entidad lo sigue resolviendo, y además el **caso de uso** responde con éxito **antes** de consultar la disponibilidad. | Un reintento por falla de red no debe fallar solo porque la fuente de disponibilidad esté caída en ese momento. |
| **A-09** | `CreateDeliveryFromOrderUseCase` reaccionaba al hecho C-01 de Pedidos. | `CreateDeliveryUseCase`, expuesto por HTTP e **idempotente por pedido** (índice único en `order_id`). | El módulo de Pedidos no se implementa en esta actividad: el endpoint simula el contrato C-01 y protege contra pedidos reenviados (riesgo R2). |
| **A-10** | El hecho *Repartidor liberado* incluía el motivo. | El motivo **no viaja** en la bandeja de salida; queda solo en el historial de la entrega. | Es texto libre del operador y podría contener datos personales (RS-03). |
| **A-11** | Fuente de disponibilidad sin definir (C-02). | Tabla `deliveries.couriers` con turno, capacidad y un **contador** de entregas activas. | Supuesto mínimo y reemplazable: cambiar la fuente solo afecta a `EfCourierAvailability` y a la guarda del repositorio. |

## Consecuencias

### Lo que ganamos
- El flujo completo funciona contra una base real, y las **76 pruebas** lo demuestran de forma repetible ([resultados](../actividad-4/resultados-y-proximos-pasos.md)).
- Las dos limitaciones que dejó la Actividad 3 quedaron **cerradas y probadas**: la capacidad entre entregas distintas y la pérdida de avisos.
- Las reglas de arquitectura se verifican automáticamente (*fitness functions*), no solo en documentos.

### Lo que aceptamos
- **La regla de capacidad queda expresada en dos lugares**: en el dominio (`CourierAvailability.IsAtCapacity`) y en la guarda SQL del repositorio. Si el negocio cambia el criterio de "ruta saturada" (término T-02), hay que cambiar ambos. El criterio CV-08 de la Actividad 2 queda **parcial**.
- **El contador `active_deliveries` puede desincronizarse** si una entrega termina por un camino que todavía no existe (entregada, fallida). Es un riesgo residual con su próximo paso.
- **Docker es obligatorio** para desarrollar y ejecutar las pruebas de integración.
- **Los avisos se entregan al menos una vez**: un mensaje puede reenviarse si el despachador cae a mitad del envío. El adaptador de notificaciones es idempotente por identificador de mensaje.

## Criterios de revisión

- Si el criterio de "ruta saturada" cambia a peso, volumen o tiempo: evaluar un agregado "Carga del repartidor" en lugar de la guarda SQL.
- Si aparece un proveedor de identidad corporativo: reemplazar la clave de API por OIDC (T-04).
- Si el despachador de la bandeja de salida se convierte en cuello de botella: varias instancias ya pueden despachar en paralelo (`SKIP LOCKED`); el siguiente paso sería un *broker* de mensajes.
