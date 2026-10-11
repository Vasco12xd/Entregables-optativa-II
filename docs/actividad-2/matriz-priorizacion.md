# Matriz de Priorización

Prioriza los requisitos del [catálogo](catalogo-requisitos.md) según su importancia para el negocio y su impacto en la arquitectura. Los requisitos con **alto impacto arquitectónico** son los que más pesan en la [decisión estructural](comparacion-alternativas.md): si se resuelven mal al inicio, corregirlos después es costoso.

## Escalas utilizadas

| Columna | Valores | Significado |
| :--- | :--- | :--- |
| **Prioridad** | Must / Should / Could | *Must*: sin esto el sistema no resuelve el problema principal. *Should*: importante, pero la operación puede arrancar sin él. *Could*: aporta valor, puede posponerse. |
| **Impacto en negocio** | Alto / Medio / Bajo | Cuánto afecta a las variables críticas de la Actividad 1: tiempo de ciclo, entregas a tiempo (OTD), exactitud de inventario y costo por entrega. |
| **Impacto en arquitectura** | Alto / Medio / Bajo | Cuánto condiciona la estructura: límites entre módulos, consistencia, integración o despliegue. |
| **Complejidad** | Alta / Media / Baja | Esfuerzo y riesgo técnico estimado para implementarlo. |

## 1. Requisitos Funcionales

| ID | Requisito (resumen) | Prioridad | Impacto en negocio | Impacto en arquitectura | Complejidad | Justificación |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **RF-01** | Registrar pedidos sin duplicados | **Must** | Alto | Alto | Media | Es la entrada de todo el flujo. Exige contratos estables por canal e idempotencia (R2). |
| **RF-02** | Validar stock por almacén | **Must** | Alto | Alto | Alta | Una sobreventa rompe la promesa al cliente (R1). Exige consistencia e integración con el ERP. |
| **RF-03** | Crear ruta y asignar repartidor | **Must** | Alto | Alto | Alta | Determina directamente las entregas a tiempo. Depende de geolocalización y de reglas de capacidad. |
| **RF-04** | Actualizar estado en cada etapa | **Must** | Alto | Alto | Media | Es la base de la "única fuente de verdad" que falta hoy. Define el ciclo de vida del pedido y la entrega. |
| **RF-09** | Integrar servicios externos | **Must** | Alto | Alto | Alta | Pagos, geolocalización y notificaciones son parte del flujo crítico. Exige adaptadores y manejo de fallas. |
| **RF-05** | Seguimiento para cliente y soporte | **Must** | Alto | Medio | Media | Resuelve la queja principal del cliente y reduce la carga de soporte. Es principalmente lectura. |
| **RF-10** | Auditoría de cambios | Should | Medio | Alto | Media | Es transversal a todos los módulos: si no se prevé desde el inicio, es difícil agregarla después. |
| **RF-07** | Gestión de incidencias | Should | Alto | Medio | Media | Soporte la necesita para responder entregas fallidas, pero el flujo principal puede arrancar sin ella. |
| **RF-11** | Eventos sin conexión del repartidor | Should | Medio | Medio | Alta | Mitiga R3. Afecta a la app móvil y a la sincronización, no a la estructura central. |
| **RF-06** | Devoluciones y reembolsos | Could | Medio | Medio | Media | Proceso de menor volumen; puede apoyarse en los módulos de pedidos e incidencias. |
| **RF-08** | Reportes de rendimiento | Could | Medio | Medio | Media | Valor analítico. Puede construirse sobre los datos ya registrados sin afectar la operación. |

## 2. Atributos de Calidad

Se priorizan con dos dimensiones: **importancia para el negocio** y **dificultad técnica**. Los atributos *Alta/Alta* son los que más influyen en la estructura elegida.

| ID | Atributo | Importancia para el negocio | Dificultad técnica | Prioridad | Escenarios |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **RNF-01** | Consistencia | Alta | Alta | **1** | EC-03, EC-04 |
| **RNF-03** | Facilidad de evolución | Alta | Alta | **2** | EC-07, EC-08 |
| **RNF-02** | Disponibilidad | Alta | Media | **3** | EC-05 |
| **RNF-08** | Resiliencia ante servicios externos | Alta | Media | **4** | EC-05, EC-06 |
| **RNF-04** | Trazabilidad | Alta | Media | **5** | EC-10 |
| **RNF-07** | Seguridad y privacidad | Alta | Media | **6** | EC-09 |
| **RNF-05** | Rendimiento | Media | Media | **7** | EC-01 |
| **RNF-06** | Escalabilidad | Media | Alta | **8** | EC-02 |
| **RNF-09** | Observabilidad | Media | Baja | **9** | EC-11 |

## 3. Conclusiones de la priorización

1. **Los requisitos *Must* se concentran en el flujo pedido → stock → asignación → seguimiento**, que corresponde a las áreas de Pedidos, Inventario, Ruteo y Entregas. Esas áreas son candidatas naturales a módulos con límites propios.
2. **Consistencia y facilidad de evolución son los atributos decisivos.** La consistencia favorece mantener las operaciones críticas dentro de un mismo proceso. La evolución exige límites claros entre áreas.
3. **La escalabilidad es importante, pero no uniforme** (RES-02). No justifica por sí sola distribuir todo el sistema. Basta con poder escalar horizontalmente y aislar más adelante el área que demuestre necesitarlo.
