# ADR-002: Diseño del dominio y de las capas para el flujo de asignación de repartidor

| Campo | Valor |
| :--- | :--- |
| **Estado** | Aceptada |
| **Fecha** | 2026-10-15 |
| **Actividad** | Actividad 3: Diseño y dominio |
| **Depende de** | [ADR-001: Monolito modular por dominios](ADR-001-estructura-monolito-modular.md) |
| **Documentos relacionados** | [Modelo de dominio](../actividad-3/modelo-dominio.md), [Invariantes y escenarios](../actividad-3/invariantes-y-escenarios.md), [Casos de uso, puertos y adaptadores](../actividad-3/casos-uso-puertos-adaptadores.md) |

## Contexto

El ADR-001 decidió construir LogiTracker como un monolito modular. Falta decidir **cómo se organiza por dentro un módulo** y **dónde viven las reglas** del primer flujo diseñado: asignar un repartidor a una entrega.

Fuerzas que condicionan el diseño:

- **Consistencia (prioridad 1):** una entrega no puede tener dos repartidores, ni un repartidor más entregas de las que puede llevar (INV-01, INV-04).
- **Facilidad de evolución (prioridad 2):** la regla de "ruta saturada" no está definida por el negocio (término T-02) y va a cambiar (escenario EC-07).
- **Fuente de disponibilidad desconocida:** no se sabe de dónde salen los turnos y la capacidad del repartidor (contrato C-02).
- **Servicios externos que fallan:** el aviso al repartidor y al cliente depende de un proveedor externo (EC-05, EC-11).
- **Reintentos de red:** un operador puede enviar dos veces la misma asignación sin saber si la primera llegó.

## Decisiones

| ID | Decisión | Alternativas descartadas |
| :--- | :--- | :--- |
| **D-01** | Cada módulo se organiza en **tres capas (Dominio, Aplicación, Infraestructura) con puertos y adaptadores**. Las dependencias apuntan siempre hacia el dominio. | Servicio único con acceso directo a BD y APIs · Más capas (CQRS completo) |
| **D-02** | La **Entrega es la raíz del agregado** y la única que puede asignar o liberar un repartidor. | Validar reglas en el caso de uso · Repartidor como raíz del agregado |
| **D-03** | La disponibilidad se obtiene por el **puerto `ICourierAvailability`**, pero **la decide el dominio** (`CourierAvailability.IsAtCapacity`). | El caso de uso llama a una API concreta · El adaptador devuelve "sí/no" |
| **D-04** | La concurrencia entre operadores se resuelve con **control optimista por versión** al guardar. | Bloqueo pesimista de la entrega |
| **D-05** | Repetir la asignación con **el mismo repartidor es idempotente**: responde con éxito sin duplicar. | Rechazar siempre, como en el ejemplo del enunciado |
| **D-06** | El resultado se publica como **hecho del dominio** por el puerto `IDeliveryEventPublisher`, con un adaptador de **bandeja de salida (*outbox*)**. | Llamar a Notificaciones dentro del caso de uso |
| **D-07** | Carpetas **por capa, con una subcarpeta por módulo** (`/src/Domain/Deliveries`…). | Carpetas por módulo, con capas dentro |
| **D-08** | El caso de uso devuelve un **resultado con código de error de negocio**; las excepciones no salen de la capa de aplicación. | Propagar excepciones del dominio hasta el controlador |

### D-01 · Tres capas con puertos y adaptadores

- **Alternativa descartada: servicio único con acceso directo.** Un `DeliveryService` que valida, consulta la base de datos y llama a Notificaciones es más rápido de escribir. Pero es el problema del `OrderService` del material del curso (Video 05): cada cambio de proveedor o de base de datos obliga a tocar las reglas, y probarlo requiere infraestructura real.
- **Alternativa descartada: CQRS completo**, con modelos separados de lectura y escritura. Es sobrediseño para un flujo con una sola escritura y una consulta simple (RES-05).
- **Trade-off aceptado:** hay más archivos e interfaces que mantener, y hay que componer las dependencias en el punto de entrada. A cambio, las reglas se prueban sin base de datos ni red, y un proveedor se cambia tocando solo su adaptador (EC-08).

### D-02 · La Entrega protege la asignación

- **Alternativa descartada: validar en el caso de uso.** Si las reglas viven en `AssignCourierUseCase`, otro caso de uso (por ejemplo, una asignación automática futura) podría olvidarlas. En la entidad, ningún camino puede saltárselas.
- **Alternativa descartada: el Repartidor como raíz del agregado**, con la lista de sus entregas. Protegería la capacidad (INV-04) de forma natural, pero el agregado crecería con cada entrega y dos operadores asignando entregas distintas al mismo repartidor chocarían entre sí.
- **Trade-off aceptado:** la regla de capacidad entre **entregas distintas** no queda protegida por el agregado (ver [riesgos](#riesgos-y-limitaciones-conocidas)).

### D-03 · Disponibilidad por puerto, decisión en el dominio

- **Alternativa descartada: llamar a una API concreta** desde el caso de uso. Acopla el flujo a una fuente que todavía no se conoce (C-02).
- **Alternativa descartada: que el adaptador responda "sí/no".** La regla de "ruta saturada" quedaría escondida en infraestructura, donde no se puede probar como regla de negocio ni encontrar fácilmente cuando cambie.
- **Trade-off aceptado:** el puerto devuelve datos (turno, carga y capacidad) en vez de una respuesta simple, de modo que el adaptador debe obtener los tres valores.

### D-04 · Control optimista de concurrencia

- **Alternativa descartada: bloqueo pesimista.** Bloquear la entrega mientras un operador decide deja a otros esperando y complica la recuperación si alguien abandona la operación.
- **Trade-off aceptado:** cuando hay conflicto, un operador recibe `concurrency_conflict` y debe volver a consultar. Es raro, porque pocos operadores asignan la misma entrega al mismo tiempo.

### D-05 · Reintento idempotente

- **Alternativa descartada: rechazar siempre** si la entrega ya tiene repartidor, como el ejemplo del enunciado. Ante una falla de red, el operador no sabe si su solicitud llegó (material del curso, Video 25). Si la reintenta y recibe un error, podría pensar que la asignación falló.
- **Trade-off aceptado:** el dominio distingue "mismo repartidor" (éxito sin cambios) de "otro repartidor" (rechazo). Es una regla adicional que debe probarse (ESC-03).

### D-06 · Publicación de hechos con bandeja de salida

- **Alternativa descartada: llamar a Notificaciones dentro del caso de uso.** Si el proveedor falla, habría que elegir entre revertir una asignación válida o perder el aviso.
- **Trade-off aceptado:** los hechos se guardan junto con la entrega y otro proceso los envía después, así que el aviso al cliente llega con unos segundos de retraso (consistencia eventual en los avisos, no en la asignación). Hasta implementarse el *outbox* en la Actividad 4, el diseño reconoce que un fallo entre guardar y publicar perdería el aviso.

### D-07 · Carpetas por capa con subcarpeta por módulo

- **Alternativa descartada: carpetas por módulo** (`/src/Deliveries/Domain`…). Hace más visibles los límites entre módulos, pero se aleja de la estructura recomendada por la actividad (`/src/Domain`, `/src/Application`, `/src/Infrastructure`), y por ahora solo hay un módulo diseñado.
- **Trade-off aceptado:** los límites entre módulos se ven menos en el árbol de carpetas y deben vigilarse con pruebas de arquitectura (criterios CV-04 y CV-05 de la Actividad 2). Revisar cuando haya tres o más módulos implementados.

### D-08 · Resultado con códigos de error de negocio

- **Alternativa descartada: propagar las excepciones** hasta el controlador. El controlador tendría que conocer las excepciones del dominio, y una regla de negocio terminaría traducida en la capa HTTP.
- **Trade-off aceptado:** el caso de uso mantiene un [catálogo de errores](../actividad-3/casos-uso-puertos-adaptadores.md#catálogo-de-errores) que debe crecer junto con las reglas.

## Consecuencias

### Lo que ganamos
- Todas las invariantes del flujo se pueden probar con objetos en memoria, sin base de datos ni red.
- La regla de "ruta saturada" vive en un único objeto de valor: cambiarla no toca otros módulos (EC-07).
- Cambiar la fuente de disponibilidad, la base de datos o el proveedor de notificaciones solo afecta a su adaptador (EC-08).
- La falla de un servicio externo no bloquea ni revierte una asignación válida (EC-05).

### Lo que aceptamos
- Más interfaces, objetos de valor y composición de dependencias que un diseño directo.
- Avisos con consistencia eventual: el cliente puede enterarse unos segundos después de la asignación.
- Disciplina del equipo para no saltarse los puertos ni acceder a datos de otros módulos.

## Riesgos y limitaciones conocidas

| Riesgo | Impacto | Tratamiento |
| :--- | :--- | :--- |
| **Capacidad entre entregas distintas (INV-04):** si dos operadores asignan **dos entregas distintas** al mismo repartidor al mismo tiempo, ambos pueden leer "4 de 5" y dejarlo con 6. El control por versión solo protege a **cada entrega**. | Medio | **Aceptado por ahora**: la asignación es manual y la probabilidad es baja. En la Actividad 4 se elige entre (a) que el adaptador de disponibilidad **reserve el cupo** de forma atómica o (b) un agregado "Carga del repartidor" con su propia versión. |
| Los supuestos de los términos ambiguos T-01 a T-09 resultan falsos. | Medio | Los cambios quedan dentro del módulo Entregas; los más probables (T-02, T-05, T-08) solo afectan a `CourierAvailability` o a la máquina de estados. |
| Pérdida del aviso entre guardar y publicar, hasta tener el *outbox* (D-06). | Bajo | Implementar el *outbox* en la Actividad 4 junto con la persistencia. |
| La disciplina de capas se pierde con el tiempo. | Medio | Pruebas automáticas de dependencias (CV-04, CV-05) en la Actividad 5. |

## Criterios de revisión

Revisar este ADR si:

- El negocio confirma que la capacidad se mide por peso, volumen o tiempo (T-02) y la regla deja de caber en `CourierAvailability`.
- Se exige que el repartidor **acepte** la asignación (T-08): aparece un estado nuevo y cambian las transiciones.
- Los conflictos de concurrencia (`concurrency_conflict`) superan un nivel que moleste a los operadores, o aparecen repartidores por encima de su capacidad.
- Se implementan tres o más módulos y la estructura de carpetas por capa dificulta ver los límites (D-07).
