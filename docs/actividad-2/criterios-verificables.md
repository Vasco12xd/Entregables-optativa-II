# Criterios Verificables de la Decisión

Estos criterios permiten comprobar, con evidencia, si la decisión del [ADR-001](../adr/ADR-001-estructura-monolito-modular.md) (monolito modular por dominios) responde a las necesidades del sistema. Cada criterio indica **cómo** se verifica y **en qué actividad** del curso se espera tener la evidencia.

## 1. Criterios de estructura

Comprueban que el monolito modular se mantiene modular y no degenera en un monolito acoplado (alternativa A).

| ID | Criterio | Cómo se verifica | Evidencia esperada |
| :--- | :--- | :--- | :--- |
| **CV-01** | Cada área de negocio (Pedidos, Inventario, Ruteo, Entregas, Incidencias) tiene un módulo propio, con responsabilidades documentadas. | Revisión del diseño y de la estructura del repositorio. | Actividad 3: modelo de dominio y diagrama de componentes. |
| **CV-02** | Cada módulo expone un contrato público y los demás lo usan solo a través de él. | Revisión de los contratos documentados entre módulos. | Actividad 3: mapa de contextos con contratos. |
| **CV-03** | Ningún módulo accede a los datos de otro módulo directamente. | Revisión del esquema de datos: cada tabla pertenece a un solo módulo. | Actividad 4: esquema de base de datos por módulo. |
| **CV-04** | Las reglas de negocio no dependen de la base de datos ni de servicios externos. | Prueba automática de dependencias: el dominio no referencia infraestructura. | Actividad 5: pruebas de arquitectura. |
| **CV-05** | No existen dependencias circulares entre módulos. | Prueba automática de dependencias entre módulos. | Actividad 5: pruebas de arquitectura. |

## 2. Criterios de calidad

Comprueban que la estructura cumple los [escenarios de calidad](escenarios-calidad.md) prioritarios.

| ID | Criterio | Escenario | Cómo se verifica | Evidencia esperada |
| :--- | :--- | :--- | :--- | :--- |
| **CV-06** | Un pedido reenviado no crea duplicados. | EC-03 | Prueba que envía dos veces el mismo pedido y cuenta los registros. | Actividad 4 |
| **CV-07** | Dos pedidos simultáneos por la última unidad no producen sobreventa. | EC-04 | Prueba concurrente sobre la reserva de inventario. | Actividad 4 |
| **CV-08** | Cambiar la regla de capacidad de ruta solo modifica el módulo responsable. | EC-07 | Revisar en el historial de commits que el cambio no toca otros módulos. | Actividad 5 |
| **CV-09** | Reemplazar un proveedor externo solo modifica su adaptador. | EC-08 | Sustituir el adaptador por una implementación falsa sin cambiar el dominio ni los casos de uso. | Actividades 4 y 5 |
| **CV-10** | La falla de la pasarela de pagos no detiene la operación. | EC-05 | Simular una pasarela que no responde y verificar que el pedido queda "pendiente de pago". | Actividad 5 |
| **CV-11** | La consulta de estado cumple el objetivo de rendimiento. | EC-01 | Prueba de carga con el volumen de SM-01; medir p95. | Actividad 5 |
| **CV-12** | Cada cambio de estado queda auditado. | EC-10 | Prueba que recorre el ciclo de un pedido y revisa su historial. | Actividad 4 |

## 3. Señales que obligan a revisar la decisión

La decisión **no es permanente**. Se revisa, y se registra un nuevo ADR, si se presenta alguna de estas señales.

### Extracción de Ruteo como servicio independiente
Ruteo es el primer candidato a separarse por tener una carga distinta en temporada alta y depender de un proveedor externo. Se evalúa su extracción **solo si se cumplen las tres señales** durante dos campañas consecutivas:

1. El p95 del cálculo de rutas supera el objetivo acordado y bloquea la confirmación de pedidos.
2. Ruteo necesita desplegar cambios con una frecuencia distinta a la de Pedidos e Inventario.
3. El equipo ya puede observar trazas, errores, reintentos y despliegues de un servicio separado sin intervención manual.

Antes de extraerlo, se aplican medidas dentro del monolito: caché de estimaciones y cola de solicitudes de ruta.

### Revisión general de la estructura
- Los volúmenes reales superan de forma sostenida los supuestos SM-01 y SM-02.
- Aparecen equipos independientes por área de negocio (el supuesto SC-01 deja de cumplirse).
- Los criterios CV-03, CV-04 o CV-05 fallan repetidamente: los límites no se están respetando y hay que reforzarlos o replantearlos.
