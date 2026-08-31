## Supuestos y Decisiones Iniciales

### Supuestos (Lo que asumimos)
1. Se asume que los canales de venta externos pueden enviar eventos o datos en formato JSON formateado.
2. Se asume que los repartidores cuentan con dispositivos móviles con soporte GPS e internet móvil.
3. Se presupone que el ERP interno cuenta con una API accesible para consultar stock base.

### Decisiones Iniciales (Lo que definimos)
1. **Plataforma Centralizada:** Toda actualización de estado del pedido debe procesarse a través de un único núcleo lógico central.
2. **Separación de Responsabilidades:** Las notificaciones y la geolocalización serán consumidas como servicios/APIs externas para no recargar la lógica principal.

### Pendientes por Confirmar
1. Definir la frecuencia con la que el ERP/CRM actualiza las diferencias físicas de stock.
2. Establecer los tiempos límite (timeouts) aceptables en la pasarela de pagos.