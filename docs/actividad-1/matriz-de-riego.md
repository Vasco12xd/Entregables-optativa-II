## Matriz de Riesgos Iniciales

| ID | Riesgo Identificado | Impacto | Estrategia de Mitigación Inicial |
| :--- | :--- | :--- | :--- |
| **R1** | **Inconsistencia de Inventario:** Desfase entre el inventario del ERP y el sistema logístico. | Alto | Implementar validaciones de stock previas a la confirmación de la orden. |
| **R2** | **Duplicación de Pedidos:** Recepción repetida de órdenes desde múltiples canales. | Alto | Asignar identificadores únicos de origen (*UUID/Idempotencia*) a cada transacción. |
| **R3** | **Pérdida de Trazabilidad por Falla Móvil:** Repartidores sin señal en zonas de baja cobertura. | Medio | Diseñar el flujo de la app del repartidor para almacenar eventos offline y sincronizar al reconectar. |
| **R4** | **Crecimiento Operativo Descontrolado:** Incremento exponencial de peticiones en temporada alta (ej. Black Friday). | Medio | Diseñar una arquitectura modular que permita escalar de forma independiente la ingesta de pedidos. |
