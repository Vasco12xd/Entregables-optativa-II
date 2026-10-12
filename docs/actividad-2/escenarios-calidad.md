# Escenarios de Calidad

Cada escenario convierte un atributo de calidad del [catálogo](catalogo-requisitos.md) en una situación concreta y **medible**. Se usa la estructura clásica de seis partes:

| Parte | Pregunta que responde |
| :--- | :--- |
| **Fuente** | ¿Quién o qué genera el estímulo? |
| **Estímulo** | ¿Qué ocurre? |
| **Entorno** | ¿En qué condiciones del sistema? |
| **Artefacto** | ¿Qué parte del sistema se ve afectada? |
| **Respuesta** | ¿Qué debe hacer el sistema? |
| **Medida** | ¿Cómo se comprueba que lo hizo bien? |

## Supuestos de medida

El caso del curso no define cifras de volumen ni de tiempos. Las siguientes metas se proponen como **supuestos por validar con el negocio**, tomando como referencia los ejemplos del enunciado de la actividad y del material del curso. Si el negocio entrega cifras reales, se reemplazan aquí y en los escenarios.

| ID | Supuesto | Referencia |
| :--- | :--- | :--- |
| **SM-01** | Volumen normal: 1.000 transacciones por hora. | Ejemplo del enunciado de la Actividad 2 |
| **SM-02** | Pico de campaña (ej. Black Friday): hasta 20 veces el volumen normal de consultas de ruta y seguimiento. | Material del curso, Video 07 |
| **SM-03** | Consulta de estado del pedido: menos de 2 segundos para el 95 % de las solicitudes (p95). | Ejemplo del enunciado y Video 08.1 |
| **SM-04** | Horario operativo con disponibilidad objetivo del 99,5 % mensual. | Propuesta propia |
| **SM-05** | Tiempo máximo de espera a la pasarela de pagos: 5 segundos. | Propuesta propia; resuelve el pendiente de la Actividad 1 |
| **SM-06** | Periodo máximo sin conexión de un repartidor que se debe tolerar: 30 minutos. | Propuesta propia |

## Escenarios

### EC-01 · Rendimiento: consulta de estado del pedido
| Parte | Descripción |
| :--- | :--- |
| Fuente | Cliente o agente de soporte |
| Estímulo | Consulta el estado de un pedido |
| Entorno | Operación normal (SM-01) |
| Artefacto | Seguimiento del pedido |
| Respuesta | Devuelve el último estado conocido y la hora de su actualización |
| Medida | p95 < 2 s (SM-03) |

### EC-02 · Escalabilidad: pico de campaña
| Parte | Descripción |
| :--- | :--- |
| Fuente | Clientes y canales de venta durante una campaña |
| Estímulo | Las consultas de ruta y seguimiento se multiplican por 20 (SM-02) |
| Entorno | Temporada alta |
| Artefacto | Aplicación completa, en especial Ruteo y Seguimiento |
| Respuesta | El sistema agrega instancias horizontalmente sin detener la operación |
| Medida | La confirmación de pedidos mantiene p95 < 2 s y no se pierde ningún pedido |

### EC-03 · Consistencia: pedido reenviado por un canal (Riesgo R2)
| Parte | Descripción |
| :--- | :--- |
| Fuente | Canal de venta |
| Estímulo | Envía dos veces el mismo pedido (mismo identificador de origen) por un reintento de red |
| Entorno | Operación normal |
| Artefacto | Registro de pedidos |
| Respuesta | Reconoce el duplicado y devuelve el pedido ya registrado, sin crear uno nuevo |
| Medida | 0 pedidos duplicados por identificador de origen |

### EC-04 · Consistencia: última unidad en stock (Riesgo R1)
| Parte | Descripción |
| :--- | :--- |
| Fuente | Dos clientes en canales distintos |
| Estímulo | Intentan confirmar al mismo tiempo un pedido por la última unidad disponible de un producto |
| Entorno | Operación normal o pico |
| Artefacto | Reserva de inventario |
| Respuesta | Solo uno de los pedidos reserva la unidad; el otro recibe "stock no disponible" |
| Medida | 0 sobreventas: la cantidad reservada nunca supera la disponible |

### EC-05 · Disponibilidad y resiliencia: la pasarela de pagos no responde
| Parte | Descripción |
| :--- | :--- |
| Fuente | Sistema de Pagos (externo) |
| Estímulo | No responde a una solicitud de validación de cobro |
| Entorno | Operación normal |
| Artefacto | Integración con pagos |
| Respuesta | Corta la espera, deja el pedido como "pendiente de pago" sin perderlo, reintenta más tarde y el resto de la operación sigue funcionando |
| Medida | Espera ≤ 5 s (SM-05); 0 pedidos perdidos; registro, asignación y seguimiento siguen disponibles |

### EC-06 · Resiliencia: repartidor sin señal (Riesgo R3)
| Parte | Descripción |
| :--- | :--- |
| Fuente | Repartidor |
| Estímulo | Registra entregas mientras está en una zona sin cobertura |
| Entorno | Sin conexión hasta 30 minutos (SM-06) |
| Artefacto | Registro de eventos de entrega |
| Respuesta | Al reconectarse, los eventos se sincronizan en el orden en que ocurrieron |
| Medida | 0 eventos perdidos o duplicados; sincronización completa en menos de 1 minuto tras reconectar |

### EC-07 · Facilidad de evolución: cambio de una regla de asignación
| Parte | Descripción |
| :--- | :--- |
| Fuente | Negocio |
| Estímulo | Cambia la regla de capacidad máxima de entregas por ruta |
| Entorno | Sistema en producción |
| Artefacto | Módulo de Entregas / Ruteo |
| Respuesta | El cambio se hace solo en el módulo responsable y se despliega sin detener la operación |
| Medida | 0 cambios en los módulos de Pedidos e Inventario; pruebas del resto de módulos sin modificaciones |

### EC-08 · Facilidad de evolución: cambio de proveedor de geolocalización
| Parte | Descripción |
| :--- | :--- |
| Fuente | Área de tecnología |
| Estímulo | Se reemplaza el proveedor de geolocalización |
| Entorno | Sistema en producción |
| Artefacto | Adaptador de geolocalización |
| Respuesta | Solo se reemplaza el adaptador; las reglas de negocio no cambian |
| Medida | 0 cambios en el dominio y en los casos de uso |

### EC-09 · Seguridad: consulta de un pedido ajeno
| Parte | Descripción |
| :--- | :--- |
| Fuente | Cliente autenticado |
| Estímulo | Intenta consultar un pedido que no le pertenece |
| Entorno | Operación normal |
| Artefacto | Seguimiento del pedido |
| Respuesta | Rechaza la consulta sin revelar datos y registra el intento |
| Medida | 100 % de los intentos rechazados y registrados; datos personales cifrados en tránsito y en reposo |

### EC-10 · Trazabilidad: reconstrucción del historial de un pedido
| Parte | Descripción |
| :--- | :--- |
| Fuente | Agente de soporte |
| Estímulo | Necesita explicar al cliente por qué su entrega se retrasó |
| Entorno | Operación normal |
| Artefacto | Auditoría de pedidos y entregas |
| Respuesta | Muestra el historial completo de cambios de estado |
| Medida | 100 % de los cambios de estado registran quién, cuándo, estado anterior y estado nuevo |

### EC-11 · Observabilidad: falla de una integración
| Parte | Descripción |
| :--- | :--- |
| Fuente | Servicio de Notificaciones (externo) |
| Estímulo | Empieza a rechazar solicitudes |
| Entorno | Operación normal |
| Artefacto | Integración con notificaciones |
| Respuesta | Registra los errores con un identificador que permita seguir cada solicitud y genera una alerta |
| Medida | Alerta generada en menos de 5 minutos desde la primera falla |

## Trazabilidad escenario → riesgo → requisito

| Escenario | Atributo | Riesgo de la Actividad 1 | Requisitos |
| :--- | :--- | :--- | :--- |
| EC-01 | Rendimiento | — | RF-05, RNF-05 |
| EC-02 | Escalabilidad | R4 | RNF-06 |
| EC-03 | Consistencia | R2 | RF-01, RNF-01 |
| EC-04 | Consistencia | R1 | RF-02, RNF-01 |
| EC-05 | Disponibilidad / Resiliencia | — | RF-09, RNF-02, RNF-08 |
| EC-06 | Resiliencia | R3 | RF-11, RNF-08 |
| EC-07 | Facilidad de evolución | R4 | RF-03, RNF-03 |
| EC-08 | Facilidad de evolución | — | RF-09, RNF-03 |
| EC-09 | Seguridad | — | RF-05, RNF-07 |
| EC-10 | Trazabilidad | — | RF-10, RNF-04 |
| EC-11 | Observabilidad | — | RF-09, RNF-09 |
