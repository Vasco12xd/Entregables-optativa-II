# Lenguaje Ubicuo del Contexto de Entregas

El lenguaje ubicuo es el conjunto de palabras que negocio y equipo técnico acuerdan usar **con el mismo significado**. Este glosario aplica dentro del contexto de **Entregas**. Otros contextos pueden usar las mismas palabras con otro significado (ver [mapa de contextos](mapa-contextos.md)).

La columna **En el código** indica el nombre que tendrá el término en el diseño. El código se escribe en inglés y la documentación en español.

## 1. Glosario

### Conceptos principales

| Término | En el código | Definición | Tipo |
| :--- | :--- | :--- | :--- |
| **Entrega** | `Delivery` | Trabajo logístico de llevar los paquetes de un pedido a una dirección. Nace cuando Pedidos declara que un pedido está listo para logística. **No es el pedido**: un pedido es una compra, una entrega es un traslado. | Entidad (raíz) |
| **Repartidor** | `Courier` | Persona que transporta las entregas. En este contexto solo interesa su identidad, su turno y su capacidad. | Entidad (referencia) |
| **Asignación** | `CourierAssignment` | Registro de que un repartidor es responsable de una entrega: quién, cuándo y quién lo decidió. | Objeto de valor |
| **Repartidor activo** | `ActiveAssignment` | El repartidor de la asignación vigente. Una entrega tiene como máximo uno. | Concepto |
| **Historial de asignaciones** | `AssignmentHistory` | Lista de todas las asignaciones que tuvo la entrega, incluidas las liberadas. | Concepto |
| **Disponibilidad del repartidor** | `CourierAvailability` | Fotografía, en un momento dado, de si el repartidor está **en turno**, cuántas entregas **lleva asignadas** y cuál es su **capacidad máxima**. | Objeto de valor |
| **Turno** | `IsOnShift` | Periodo en que el repartidor está trabajando y puede recibir entregas. | Atributo |
| **Capacidad máxima** | `MaxDeliveries` | Número máximo de entregas activas que un repartidor puede llevar al mismo tiempo. | Atributo |
| **Ruta saturada** | `IsAtCapacity` | El repartidor ya lleva tantas entregas activas como su capacidad máxima. | Regla |
| **Dirección de entrega** | `DeliveryAddress` | Lugar al que se lleva el paquete: calle, ciudad y código postal. | Objeto de valor |
| **Ventana de entrega** | `DeliveryWindow` | Intervalo de tiempo en el que el cliente espera recibir el paquete. El inicio siempre es anterior al fin. | Objeto de valor |
| **Referencia de pedido** | `OrderId` | Identificador del pedido que originó la entrega. Entregas lo guarda como referencia, no conoce el contenido comercial del pedido. | Objeto de valor |
| **Operador** | `OperatorId` | Usuario interno que decide la asignación. Queda registrado por trazabilidad. | Objeto de valor |

### Acciones

| Término | En el código | Definición |
| :--- | :--- | :--- |
| **Asignar repartidor** | `AssignCourier` | Hacer responsable a un repartidor de una entrega pendiente, si cumple las reglas. |
| **Liberar repartidor** | `ReleaseCourier` | Quitar el repartidor activo de una entrega asignada, con un motivo obligatorio. La entrega vuelve a estar pendiente. |
| **Publicar hecho** | `PublishAsync` | Avisar a otras partes que algo ya ocurrió (por ejemplo, *Repartidor asignado*). |

### Estados de la entrega

| Estado | En el código | Significado | ¿Tiene repartidor activo? |
| :--- | :--- | :--- | :---: |
| **Pendiente de asignación** | `PendingAssignment` | La entrega existe pero nadie es responsable de llevarla. | No |
| **Asignada** | `Assigned` | Un repartidor es responsable, pero aún no ha salido con el paquete. | Sí |
| **En camino** | `InTransit` | El repartidor recogió el paquete y va hacia la dirección. | Sí |
| **Entregada** | `Delivered` | El cliente recibió el paquete y existe evidencia. Estado final. | Sí (histórico) |
| **Fallida** | `Failed` | El repartidor no pudo entregar (cliente ausente, dirección errónea…). | No |
| **Cancelada** | `Cancelled` | La entrega se detuvo antes de salir. Estado final. | No |

### Hechos del dominio

| Hecho | En el código | Cuándo ocurre | Quién lo usa |
| :--- | :--- | :--- | :--- |
| **Repartidor asignado** | `CourierAssigned` | Una entrega pasa a *Asignada*. | Notificaciones (avisar al repartidor y al cliente), Pedidos (vista de soporte), Auditoría |
| **Repartidor liberado** | `CourierReleased` | Se quita el repartidor de una entrega. | Notificaciones, Auditoría |

## 2. Términos ambiguos por confirmar

Estos términos **no tienen una definición única en el caso del curso**. Para poder diseñar, se adopta un supuesto explícito, pero cada uno debe confirmarse con quienes operan el proceso. Si la respuesta real es distinta, el cambio queda dentro del contexto de Entregas.

| ID | Término | Por qué es ambiguo | Supuesto adoptado | A quién confirmar |
| :--- | :--- | :--- | :--- | :--- |
| **T-01** | **Ruta** | Para Ruteo es un trayecto geográfico (origen, destino, distancia). Para operación es "las entregas que lleva un repartidor en su turno". | En Entregas se habla de **carga del repartidor**. "Ruta" queda reservada para Ruteo. | Operación logística |
| **T-02** | **Ruta saturada** | Puede medirse por número de entregas, peso, volumen del vehículo o tiempo estimado de recorrido. | Se mide por **número de entregas activas** frente a una capacidad máxima. | Operación logística |
| **T-03** | **Disponible** | Para Inventario es "hay stock", para Ruteo "hay ruta viable", para Pedidos "se puede comprar" (Video 06). | En Entregas, un repartidor disponible está **en turno y no ha alcanzado su capacidad**. | Equipo de producto |
| **T-04** | **Repartidor activo** | Puede significar "repartidor que trabaja hoy" o "repartidor asignado ahora a esta entrega". | Significa **el repartidor de la asignación vigente** de una entrega. Para el otro sentido se usa "en turno". | Equipo de producto |
| **T-05** | **Reasignar** | No se sabe si se permite, quién puede hacerlo ni hasta qué estado. | Se permite **solo desde Asignada**, liberando primero con motivo. En camino ya no se reasigna desde este flujo. | Operación logística y soporte |
| **T-06** | **Capacidad máxima** | No se sabe si es igual para todos, si depende del vehículo o si la fija un supervisor por turno. | Es un número por repartidor, entregado por el puerto de disponibilidad. Entregas no decide cómo se calcula. | Operación logística |
| **T-07** | **Quién asigna** | El caso base no dice si asigna el operador de tienda, el administrador de almacén o un proceso automático. | Un **operador con permiso de asignación**. Una asignación automática futura usaría el mismo caso de uso. | Operación logística |
| **T-08** | **Asignada vs. aceptada** | No se sabe si el repartidor debe aceptar la entrega antes de considerarla suya. | La asignación es **efectiva sin aceptación**. Si se requiere aceptación, se agregaría un estado nuevo. | Operación logística |
| **T-09** | **Confirmado** | En Pedidos significa "compra aceptada"; en Entregas podría confundirse con "entrega asignada" o "entrega completada" (Video 17). | En Entregas **no se usa "confirmado"**. Se usan los estados de la tabla anterior. | Equipo de producto |
