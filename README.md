# Plataforma de Gestión Logística de Última Milla (LogiTracker)

## Resumen Ejecutivo

* **Nombre del producto:** LogiTracker - Sistema de Gestión Logística de Última Milla.
* **Problema principal:** Dispersión de información de pedidos entre múltiples canales de venta, inventarios distribuidos y falta de trazabilidad en tiempo real de las entregas. Esto genera retrasos, duplicidad de datos e insatisfacción del cliente.
* **Usuarios principales:** Clientes finales, Operadores de Tienda, Administradores de Almacén, Repartidores y Equipo de Soporte al Cliente.
* **Objetivo del sistema:** Centralizar el ciclo de vida del pedido (desde el registro hasta la entrega final) para garantizar trazabilidad, sincronización de inventario e integración eficiente de rutas.

## Alcance del Proyecto

### Dentro del Alcance
* Recepción e ingesta centralizada de pedidos desde canales externos.
* Módulo de validación y consulta de disponibilidad de inventario distribuido.
* Módulo de asignación y seguimiento de rutas para repartidores.
* Panel de visibilidad de estado en tiempo real para clientes y soporte.
* Gestión básica de incidencias en la entrega.

### Fuera del Alcance
* Construcción de las tiendas digitales o canales de venta: LogiTracker solo recibe los pedidos que estos generan.
* Procesamiento propio de pagos: el cobro se delega al Sistema de Pagos externo.
* Motor cartográfico propio: rutas y coordenadas se obtienen del Servicio de Geolocalización.
* Infraestructura propia de envío de SMS o correos: se delega al Servicio de Notificaciones.
* Reemplazo del ERP/CRM: facturación, contabilidad y datos maestros de clientes siguen en el sistema interno.

## Documentación por Actividad

### Actividad 1: Diagnóstico y contexto
* [Diagrama de contexto](docs/actividad-1/diagrama-contexto.md)
* [Análisis del problema](docs/actividad-1/analisis-problema.md)
* [Matriz de riesgos](docs/actividad-1/matriz-de-riesgo.md)
* [Supuestos y decisiones iniciales](docs/actividad-1/supuestos-y-decisiones-iniciales.md)