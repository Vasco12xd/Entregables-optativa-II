# Catálogo de Requisitos

Este catálogo traduce el problema de la [Actividad 1](../actividad-1/analisis-problema.md) en requisitos verificables. Cada requisito indica su **origen** para mantener la trazabilidad con el caso base y con los riesgos ya identificados en la [matriz de riesgos](../actividad-1/matriz-de-riesgo.md).

La priorización de estos requisitos está en la [matriz de priorización](matriz-priorizacion.md).

## 1. Requisitos Funcionales (RF)

Describen **lo que el sistema debe hacer**.

| ID | Requisito | Actor principal | Origen |
| :--- | :--- | :--- | :--- |
| **RF-01** | Registrar pedidos con múltiples líneas de producto recibidos desde distintos canales de venta, sin crear duplicados si un canal reenvía el mismo pedido. | Operador de Tienda / Canal de venta | Caso base RF1 · Riesgo R2 |
| **RF-02** | Validar el stock disponible por almacén y punto de venta antes de confirmar un pedido. | Administrador de Almacén | Caso base RF2 · Riesgo R1 |
| **RF-03** | Crear una ruta de entrega y asignar cada entrega a un repartidor. | Operador / Sistema | Caso base RF3 |
| **RF-04** | Actualizar el estado del pedido y de la entrega en cada etapa del ciclo (registrado, confirmado, preparado, asignado, en camino, entregado, fallido). | Repartidor / Almacén | Caso base RF4 |
| **RF-05** | Permitir el seguimiento del pedido a clientes y al equipo de soporte, mostrando el último estado conocido y su hora. | Cliente / Soporte | Caso base RF5 |
| **RF-06** | Gestionar devoluciones y reembolsos básicos asociados a un pedido válido y a un motivo concreto. | Operador / Soporte | Caso base RF6 |
| **RF-07** | Registrar incidencias con prioridad, causa, responsable y resolución. | Soporte / Repartidor | Caso base RF7 |
| **RF-08** | Generar reportes de rendimiento por zona, repartidor y tipo de entrega. | Operador / Administración | Caso base RF8 |
| **RF-09** | Integrarse con los servicios externos de pagos, geolocalización, notificaciones y con el ERP/CRM interno. | Sistema | Caso base RF9 · Diagrama de contexto |
| **RF-10** | Mantener una auditoría de los cambios importantes de la operación (quién, qué, cuándo, valor anterior y nuevo). | Sistema / Soporte | Caso base RF10 |
| **RF-11** | Permitir que el repartidor registre eventos de entrega sin conexión y sincronizarlos al reconectarse, sin perderlos ni duplicarlos. | Repartidor | Riesgo R3 |

## 2. Requisitos No Funcionales / Atributos de Calidad (RNF)

Describen **qué tan bien** debe comportarse el sistema. Cada uno se vuelve medible en los [escenarios de calidad](escenarios-calidad.md).

| ID | Atributo | Descripción | Origen |
| :--- | :--- | :--- | :--- |
| **RNF-01** | Consistencia | Las operaciones críticas (reserva de stock, asignación de repartidor, registro de pedido) no deben producir sobreventas, asignaciones dobles ni pedidos duplicados. | Caso base · Riesgos R1 y R2 |
| **RNF-02** | Disponibilidad | La operación crítica (registro de pedidos, asignación y actualización de entregas) debe estar disponible durante el horario operativo. | Caso base |
| **RNF-03** | Facilidad de evolución | Cambiar una regla de negocio o un proveedor externo no debe requerir modificar toda la aplicación. | Caso base · Restricción RES-04 |
| **RNF-04** | Trazabilidad | Todo cambio de estado de un pedido o entrega debe poder reconstruirse. | Caso base · Problema principal |
| **RNF-05** | Rendimiento | Las consultas de estado deben responder en tiempos razonables para clientes y soporte. | Caso base |
| **RNF-06** | Escalabilidad | El sistema debe absorber picos de demanda (temporadas altas) escalando horizontalmente. | Caso base · Riesgo R4 |
| **RNF-07** | Seguridad y privacidad | Los datos personales y las transacciones deben estar protegidos; cada actor solo accede a lo que le corresponde. | Caso base |
| **RNF-08** | Resiliencia ante servicios externos | La falla de un servicio externo (pagos, geolocalización, notificaciones, ERP) no debe detener la operación completa ni perder información. | Supuestos y pendientes de la Actividad 1 |
| **RNF-09** | Observabilidad | Los errores y fallas de integración deben registrarse y poder detectarse rápidamente. | Caso base |

## 3. Restricciones del Negocio (RES)

Condiciones que **no se eligen**: limitan las alternativas posibles.

| ID | Restricción | Consecuencia para la arquitectura |
| :--- | :--- | :--- |
| **RES-01** | La operación actual ya está en producción y no puede detenerse para reescribir sistemas. | La solución debe poder introducirse de forma gradual, convivir con lo existente e integrarse al ERP/CRM actual. |
| **RES-02** | El crecimiento es acelerado pero no uniforme. | No todas las áreas necesitan escalar igual; la estructura debe permitir aislar más adelante la que lo requiera. |
| **RES-03** | Existen varios canales de venta y varios puntos de operación. | Se requieren contratos de entrada estables y mecanismos contra duplicados. |
| **RES-04** | El sistema debe poder evolucionar sin caos. | Se necesitan límites claros entre áreas de negocio y dependencias controladas. |
| **RES-05** | La empresa prioriza tiempo de entrega y confiabilidad antes que una arquitectura "perfecta". | Se favorece la alternativa más simple que cumpla los atributos de calidad prioritarios. |

## 4. Supuestos de Contexto

Supuestos necesarios para tomar la decisión estructural. **Deben validarse con el negocio**; si alguno resulta falso, la decisión se revisa según los [criterios verificables](criterios-verificables.md).

| ID | Supuesto |
| :--- | :--- |
| **SC-01** | El desarrollo inicial lo realiza un único equipo pequeño, sin equipos independientes por área de negocio. |
| **SC-02** | La empresa aún no cuenta con automatización madura de despliegue ni monitoreo distribuido. |
| **SC-03** | Los supuestos de la Actividad 1 siguen vigentes: los canales envían datos en JSON, los repartidores tienen GPS e internet móvil y el ERP expone una API de consulta de stock. |
