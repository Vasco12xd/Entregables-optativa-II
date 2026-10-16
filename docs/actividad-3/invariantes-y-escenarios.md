# Invariantes y Escenarios de Aceptación

Una **invariante** es una regla que siempre debe cumplirse, sin importar la pantalla, la base de datos o el servicio externo que se use. Los **escenarios de aceptación** (formato Dado-Cuando-Entonces) describen ejemplos concretos que permiten comprobar cada invariante, y en la Actividad 4 se convertirán en pruebas automatizadas.

## 1. Invariantes

| ID | Invariante | Quién la protege | Escenarios |
| :--- | :--- | :--- | :--- |
| **INV-01** | Una entrega tiene **como máximo un repartidor activo**. | `Delivery.AssignCourier` | ESC-02, ESC-08 |
| **INV-02** | Solo se asigna repartidor a una entrega **pendiente de asignación**. | `Delivery.AssignCourier` | ESC-07 |
| **INV-03** | No se asigna una entrega a un repartidor **fuera de turno**. | `Delivery.AssignCourier` con `CourierAvailability` | ESC-04 |
| **INV-04** | No se asigna una entrega a un repartidor cuya **capacidad está alcanzada** ("ruta saturada"). | `CourierAvailability.IsAtCapacity` | ESC-05 |
| **INV-05** | Toda asignación registra **repartidor, operador y momento**. | `CourierAssignment` | ESC-01 |
| **INV-06** | Estado y repartidor son coherentes: *Asignada* y *En camino* **siempre** tienen repartidor activo; *Pendiente de asignación* **nunca** lo tiene. | `Delivery` (métodos de transición) | ESC-01, ESC-10 |
| **INV-07** | Liberar un repartidor exige un **motivo**. | `Delivery.ReleaseCourier`, `ReleaseReason` | ESC-10, ESC-11 |
| **INV-08** | La ventana de entrega tiene un **inicio anterior al fin**. | `DeliveryWindow` | ESC-12 |
| **INV-09** | Una entrega *En camino*, *Entregada*, *Fallida* o *Cancelada* **no cambia de repartidor** mediante este flujo. | `Delivery` | ESC-07 |
| **INV-10** | Repetir la misma asignación (mismo repartidor) **no crea una segunda asignación**. | `Delivery.AssignCourier` | ESC-03 |

> **Nota sobre concurrencia:** las invariantes INV-01 e INV-04 pueden romperse si dos operaciones ocurren al mismo tiempo, aunque cada una por separado sea válida. INV-01 se protege con control de versión al guardar la entrega (ESC-08). INV-04 entre **entregas distintas** del mismo repartidor es una limitación conocida, registrada en el [ADR-002](../adr/ADR-002-diseno-flujo-asignacion.md#riesgos-y-limitaciones-conocidas).

## 2. Escenarios de aceptación

Los ejemplos usan datos concretos para evitar ambigüedades: la entrega **E-245**, los repartidores **Ana** y **Luis**, y la operadora **Marta**.

### Camino exitoso

```gherkin
Escenario: ESC-01 Asignar un repartidor disponible a una entrega pendiente
  Dado que la entrega E-245 está "Pendiente de asignación"
  Y que Ana está en turno con 3 entregas activas y capacidad máxima de 5
  Cuando la operadora Marta asigna a Ana a la entrega E-245
  Entonces la entrega E-245 queda "Asignada" con Ana como repartidora activa
  Y la asignación registra que la hizo Marta y la hora en que ocurrió
  Y se publica el hecho "Repartidor asignado" para E-245
```

### Reglas de la asignación

```gherkin
Escenario: ESC-02 Rechazar la asignación si la entrega ya tiene otro repartidor
  Dado que la entrega E-245 está "Asignada" a Luis
  Cuando la operadora Marta intenta asignar a Ana a la entrega E-245
  Entonces la asignación es rechazada con el motivo "la entrega ya tiene repartidor asignado"
  Y Luis sigue siendo el repartidor activo de E-245
  Y no se consulta la disponibilidad de Ana

Escenario: ESC-03 Repetir la misma asignación no la duplica
  Dado que la entrega E-245 está "Asignada" a Ana
  Cuando la operadora Marta vuelve a asignar a Ana a la entrega E-245 por un reintento de red
  Entonces la operación responde con éxito
  Y el historial de E-245 sigue teniendo una sola asignación
  Y no se publica un nuevo hecho "Repartidor asignado"

Escenario: ESC-04 Rechazar la asignación a un repartidor fuera de turno
  Dado que la entrega E-245 está "Pendiente de asignación"
  Y que Ana no está en turno
  Cuando la operadora Marta asigna a Ana a la entrega E-245
  Entonces la asignación es rechazada con el motivo "repartidor no disponible"
  Y la entrega E-245 sigue "Pendiente de asignación"

Escenario: ESC-05 Rechazar la asignación si la ruta del repartidor está saturada
  Dado que la entrega E-245 está "Pendiente de asignación"
  Y que Ana está en turno con 5 entregas activas y capacidad máxima de 5
  Cuando la operadora Marta asigna a Ana a la entrega E-245
  Entonces la asignación es rechazada con el motivo "capacidad del repartidor alcanzada"
  Y la entrega E-245 sigue "Pendiente de asignación"

Escenario: ESC-06 Rechazar la asignación de una entrega inexistente
  Dado que no existe ninguna entrega con identificador E-999
  Cuando la operadora Marta asigna a Ana a la entrega E-999
  Entonces la asignación es rechazada con el motivo "entrega no encontrada"

Escenario: ESC-07 Rechazar la asignación de una entrega que ya salió
  Dado que la entrega E-245 está "En camino" con Luis
  Cuando la operadora Marta intenta asignar a Ana a la entrega E-245
  Entonces la asignación es rechazada con el motivo "la entrega no admite asignación en su estado actual"
  Y Luis sigue siendo el repartidor activo de E-245
```

### Concurrencia y fallas

```gherkin
Escenario: ESC-08 Dos operadores asignan la misma entrega al mismo tiempo
  Dado que la entrega E-245 está "Pendiente de asignación"
  Y que Ana y Luis están en turno y con capacidad disponible
  Cuando la operadora Marta asigna a Ana y, al mismo tiempo, otro operador asigna a Luis
  Entonces solo una de las dos asignaciones queda guardada
  Y la otra solicitud recibe el motivo "la entrega cambió, vuelva a consultarla"
  Y E-245 tiene un solo repartidor activo

Escenario: ESC-09 El servicio de disponibilidad no responde
  Dado que la entrega E-245 está "Pendiente de asignación"
  Y que el servicio de disponibilidad de repartidores no responde
  Cuando la operadora Marta asigna a Ana a la entrega E-245
  Entonces la asignación no se realiza
  Y la entrega E-245 sigue "Pendiente de asignación"
  Y se informa un error temporal que permite reintentar
```

### Liberación y objetos de valor

```gherkin
Escenario: ESC-10 Liberar al repartidor con motivo devuelve la entrega a pendiente
  Dado que la entrega E-245 está "Asignada" a Ana
  Cuando la operadora Marta libera a Ana con el motivo "vehículo averiado"
  Entonces la entrega E-245 queda "Pendiente de asignación" sin repartidor activo
  Y el historial conserva la asignación de Ana con la hora de liberación y su motivo

Escenario: ESC-11 No se puede liberar a un repartidor sin motivo
  Dado que la entrega E-245 está "Asignada" a Ana
  Cuando la operadora Marta intenta liberar a Ana sin indicar un motivo
  Entonces la liberación es rechazada con el motivo "se requiere un motivo"
  Y Ana sigue siendo la repartidora activa de E-245

Escenario: ESC-12 No se acepta una ventana de entrega invertida
  Dado un pedido listo para logística con ventana de entrega de 18:00 a 14:00
  Cuando se intenta crear la entrega correspondiente
  Entonces la entrega no se crea
  Y se informa que la ventana de entrega no es válida
```

## 3. Trazabilidad con las actividades anteriores

| Escenario | Invariante | Escenario de calidad (Actividad 2) | Riesgo (Actividad 1) |
| :--- | :--- | :--- | :--- |
| ESC-01 | INV-05, INV-06 | EC-10 Trazabilidad | — |
| ESC-02, ESC-08 | INV-01 | EC-03/EC-04 Consistencia | R2 Duplicación |
| ESC-03 | INV-10 | EC-03 Consistencia (reintentos) | R2 Duplicación |
| ESC-05 | INV-04 | EC-07 Evolución de la regla de capacidad | R4 Crecimiento |
| ESC-09 | — | EC-05 Resiliencia ante externos | — |
| ESC-10, ESC-11 | INV-07 | EC-10 Trazabilidad | — |
