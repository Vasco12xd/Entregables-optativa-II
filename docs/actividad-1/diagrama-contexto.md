## Diagrama de Contexto

Muestra a LogiTracker como una sola caja, los actores que lo usan, los sistemas externos con los que se integra y la información que cruza el límite del sistema.

```mermaid
flowchart TB
    %% Actores humanos
    cliente(["👤 Cliente"])
    operador(["👤 Operador de Tienda"])
    almacen(["👤 Administrador de Almacén"])
    repartidor(["👤 Repartidor"])
    soporte(["👤 Soporte al Cliente"])

    %% Límite del sistema
    subgraph limite["Límite del sistema"]
        logi["<b>LogiTracker</b><br/>Plataforma centralizada de gestión logística de última milla"]
    end

    %% Sistemas externos
    pagos[["💳 Sistema de Pagos"]]
    geo[["🗺️ Servicio de Geolocalización"]]
    notif[["✉️ Servicio de Notificaciones"]]
    erp[["🏢 ERP / CRM Interno"]]

    cliente -- "Consulta estado del pedido / Reporta incidencias" --> logi
    operador -- "Registra y revisa pedidos" --> logi
    almacen -- "Confirma preparación y stock" --> logi
    repartidor -- "Actualiza estado de entrega / Ubicación GPS" --> logi
    soporte -- "Consulta historial y gestiona incidencias" --> logi

    logi -- "Valida cobros" --> pagos
    logi -- "Consulta rutas y coordenadas" --> geo
    logi -- "Solicita envío de SMS / Emails" --> notif
    logi <-- "Sincroniza inventario y datos de clientes" --> erp

    classDef sistema fill:#1f4e79,stroke:#0b2540,color:#ffffff
    classDef externo fill:#6b6b6b,stroke:#3a3a3a,color:#ffffff
    class logi sistema
    class pagos,geo,notif,erp externo
```

**Leyenda**

| Elemento | Significado |
| :--- | :--- |
| 👤 Óvalo | Actor humano que usa el sistema |
| Caja azul | Sistema que se construye (LogiTracker) |
| Caja gris con doble borde | Sistema externo que no se construye, solo se integra |
| Recuadro "Límite del sistema" | Todo lo que está dentro es responsabilidad de LogiTracker |
| Flecha | Dirección de la información o de la solicitud |

> Versión original en imagen: [Diagrama de contextos.png](Diagrama%20de%20contextos.png)
