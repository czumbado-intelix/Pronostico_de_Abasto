# PronosticosAbasto — Documento de diseño

App nativa de Windows (WinUI 3 / .NET 8) para abasto. Compara un **forecast/demanda** contra el **inventario eFlow** y dice **qué hay que traer de las bodegas satelitales (externas)** para cubrir lo que no alcanza la bodega principal, por empresa (EPA, Cofersa).

---

## Guía visual vigente

La identidad visual actual usa una paleta mínima para que la app se sienta operativa, limpia y fácil de leer en bodega/oficina:

| Uso | Color |
|-----|-------|
| Acción primaria, barras, énfasis y estados activos | `#00A88F` |
| Fondos, tarjetas, diálogos y tablas | `#FFFFFF` |
| Texto normal, encabezados, números y etiquetas | `#000000` |
| Texto sobre fondo verde sólido | `#FFFFFF` |

### Reglas de color

- Todo texto de lectura va en negro: títulos, columnas, valores, etiquetas, filtros, formularios y comentarios.
- Cuando un elemento tenga fondo verde sólido `#00A88F`, su texto e iconos van en blanco.
- El verde se reserva para acciones principales, estados activos, barras del histograma, líneas de énfasis e iconos de apoyo.
- Los botones secundarios quedan blancos con texto negro y borde verde suave.
- Las superficies no usan fondos oscuros ni colores adicionales como tema dominante.
- El icono de cada módulo debe repetirse en el menú lateral y en el encabezado principal del módulo, dentro de una pastilla verde con icono blanco.

### Botones

- Botón primario: fondo `#00A88F`, texto `#FFFFFF`, radio de 4 px, peso semibold.
- Botón secundario: fondo blanco, texto negro, borde verde translúcido.
- Botones transparentes de agregar o editar: texto negro con icono verde cuando el icono sea el elemento de acción.
- Los estados hover/pressed deben mantener suficiente contraste; si el fondo continúa verde, el texto debe seguir blanco.
- Los botones primarios internos de `ContentDialog`, como **Guardar** en Ajustes, deben usar el mismo estilo primario verde/blanco; no deben heredar el azul del sistema.
- No usar `DefaultButton = Primary` en diálogos si eso fuerza `AccentButtonStyle`; si WinUI aplica `AccentButtonStyle`, sus recursos `AccentButtonBackground` y `AccentButtonForeground` deben estar redefinidos a verde/blanco.

### Tablas y datos

- Las tablas priorizan lectura: texto negro, encabezados compactos y fondos blancos.
- Los filtros tipo Excel deben sentirse como controles de trabajo, no como tarjetas decorativas.
- Cada columna filtrable usa el mismo patron tipo Excel: ordenar A-Z/Z-A, buscar valores, seleccionar todo, lista con checks y botones Aceptar/Cancelar.
- Los números importantes pueden usar peso semibold, pero no deben depender del color para entenderse.
- La descripción del artículo debe mostrarse siempre que exista en inventario o forecast.

### Movimiento

- La app usa animaciones sutiles de entrada, reposición y cambios de lista.
- La animación debe ayudar a entender que una fila, tarjeta o vista cambió; no debe distraer del análisis de abasto.
- Las transiciones recomendadas son cortas y discretas: `EntranceThemeTransition`, `RepositionThemeTransition` y `AddDeleteThemeTransition`.

### Comparativa e histograma

- La vista Comparativa e histograma usa la misma paleta: tabla blanca, texto negro y barras verdes.
- El histograma de cobertura OLO se lee como conteo exacto por semana: un articulo que alcanza 7 semanas solo aparece en la barra `7`, no en las barras anteriores.
- Las filas con comentario `no solicita` no se grafican en el histograma.
- En Comparativa, si todas las semanas futuras del articulo son 0, debe quedar como `no solicita`; si el articulo pide al menos 1 unidad en el forecast futuro, las semanas/meses con forecast 0 tambien cuentan como cobertura.
- Si un articulo viene repetido en el forecast, Comparativa suma todas las filas del mismo articulo por semana/mes antes de calcular cobertura.
- Comparativa e histograma no contabilizan zonas configuradas en `Zonas Excluidas del Inventario`; por defecto incluye ZA23, ZA30, ZA38, ZA39, ZA40 y ZA55.
- Las barras verdes del histograma deben conservar texto o etiquetas negras si están fuera de la barra; si una etiqueta queda dentro del verde, debe ser blanca.
- La lista base de Comparativa muestra articulos con `Inv SERVICA > 0`; los articulos con `Inv SERVICA = 0` no son necesarios para esta revision. Los articulos con `Inv OLO = 0` si deben poder aparecer cuando tengan inventario en SERVICA, porque representan falta de cobertura desde OLO.

---

## 1. Propósito y flujo que implementa

La app codifica un diagrama de decisión que usa el operador de abasto:

```
IPA manda forecast ─► SAC baja inventario eFlow ─► comparar (forecast vs inventario)
        │
        ▼
 ¿el PRINCIPAL cubre el forecast?
        ├─ SÍ ─► listo, no hay que traer nada
        └─ NO ─► ¿hay en BODEGA EXTERNA (satélite)?
                   ├─ SÍ ─► traer lo disponible (traslado) para cubrir el faltante
                   └─ NO ─► escalar a compra (no hay de dónde traer)
```

El "trabajo" del operador es ver **qué traer**, por eso la app está orientada a ese resultado y no a un dashboard genérico.

---

## 2. Principios de arquitectura

- **Separación lógica / UI.** Toda la lógica de negocio vive en un proyecto puro y testeable (`PronosticosAbasto.Core`), sin dependencias de WinUI. La UI (`PronosticosAbasto`) sólo orquesta y presenta.
- **MVVM** con CommunityToolkit.Mvvm (`[ObservableProperty] public partial`, `[RelayCommand]`, `[NotifyCanExecuteChangedFor]`).
- **El cálculo no se duplica en la UI.** El analizador produce un modelo completo; las vistas sólo lo proyectan.
- **Datos primero, UI después.** Los formatos de Excel se auto-detectan en el Core; la UI no conoce columnas de Excel.
- **Todo verificable.** El Core tiene pruebas xUnit; la UI se valida con build limpio (x:Bind se compila) + corridas reales.
- **Las tablas se declaran, no se programan.** Las cuatro tablas filtrables (Análisis, Traslados, Expediciones, Comparativa) declaran sus columnas una sola vez en `ViewModels/MainPageViewModel.Tables.cs`: texto visible, filtro y, si aplica, clave numérica de orden. `Core/Tables/TableView<TRow>` recorre esa declaración para repoblar las opciones del panel tipo Excel, filtrar y ordenar. Agregar una columna es una línea, no tres métodos. El orden se pide con la **instancia del filtro**, no con una clave de texto, así que no hay strings en el XAML que puedan desalinearse en silencio.

---

## 3. Estructura de la solución

```
PronosticosAbasto.Core/         Lógica pura (sin WinUI). ClosedXML para Excel.
  Analysis/                     Modelo de dominio + analizadores
  IO/                           Parseo y exportación de Excel
  Configuration/                Defaults de zonas satélite por empresa
  Tables/                       Motor de filtrado y orden de las tablas
  Storage/                      Stores en disco (JSON en %LOCALAPPDATA%)
PronosticosAbasto/              App WinUI 3 (UI + MVVM)
  ViewModels/                   MainPageViewModel + VMs de fila/sesión
  Services/                     File picker y diálogos (lo único que toca WinUI)
  MainPage.xaml(.cs)            Pantalla única con 3 vistas
  App.xaml.cs                   Arranque + manejador global de excepciones
PronosticosAbasto.Core.Tests/   xUnit (121 pruebas)
```

> El solution file es `PronosticosAbasto.slnx`; también se puede compilar cada
> proyecto por separado (ver §11).

---

## 4. Modelo de dominio (`PronosticosAbasto.Core/Analysis`)

| Tipo | Rol |
|------|-----|
| `InventoryPosition(Article, StorageZone, Quantity)` | Una posición de inventario (artículo + zona + cantidad). |
| `ForecastEntry` | Demanda de un artículo en una semana/periodo. |
| `ZoneInventoryDetail(StorageZone, Quantity, IsSatellite)` | Stock por zona, ya clasificado principal/satélite. |
| `CoverageStatus` | Semáforo: `Critical` (rojo) / `Warning` (amarillo) / `Healthy` (verde). |
| `CoverageThresholds(RedPercent, HealthyPercent)` | Umbrales del semáforo. Default `(0, 100)`: rojo = sin stock, verde = cubre el 100%, amarillo = en medio. |
| `AnalysisHorizon` (`HorizonUnit` Weeks/Months) | Horizonte: 4 semanas, o 1/2/3 **meses corridos** (rolling desde la semana elegida). |
| `TransferRecommendationState` | `NotRequired` / `Suggested` / `Partial` / `Unavailable`. |
| `ArticleAnalysisResult` | Resultado por artículo: principal, satélite, diferencia, estado, **cantidad a traer**, **faltante restante**, zonas satélite. |
| `WeeklyAnalysisResult` | Resultado completo: artículos + conteos + metadatos del horizonte. |
| `ArticleInventorySummary` | Resumen por artículo (principal / satélite / zonas), producido por el clasificador de zonas. |

### Matemática del traslado (núcleo del flujo)

Para cada artículo (en `WeeklyForecastAnalyzer`) y cada línea (en `ExpedicionesAnalyzer`):

```
principalShortage             = max(0, demanda − principal)
transferSuggestionQuantity    = min(principalShortage, satéliteDisponible)   // "A traer"
remainingShortageAfterTransfer= max(0, principalShortage − transferSuggestion) // "Pendiente"
```

Estado: sin faltante → `NotRequired`; faltante y sin satélite → `Unavailable`; faltante con satélite y pendiente 0 → `Suggested`; pendiente > 0 → `Partial`.

### Clasificación de zonas (`InventoryZoneClassifier`)

Agrupa el inventario por artículo y marca cada zona como **principal** o **satélite** según las zonas externas configuradas por empresa. Normaliza claves de zona (mayúsculas + colapsa espacios). Si la lista de zonas externas está **vacía**, trata **todo** como satélite (caso degenerado, ver §10). Este helper es compartido por `WeeklyForecastAnalyzer` (que mantiene su propia copia histórica de la lógica) y por `ExpedicionesAnalyzer`.

---

## 5. Analizadores

- **`WeeklyForecastAnalyzer`** — el principal. `Analyze(inventory, forecast, selectedWeek, horizon, thresholds, satelliteZones)`. Resuelve el horizonte (toma N semanas, o suma meses corridos con marca de "incompleto"), agrupa inventario por artículo/zona, agrupa forecast por artículo dentro del horizonte, calcula semáforo + traslado, y devuelve `WeeklyAnalysisResult`. Alimenta la vista **Traslados**.
- **`ExpedicionesAnalyzer`** — gemelo ligero para la sección **Expediciones**. `Analyze(lines, inventory, satelliteZones)`. Aplica la **misma** matemática de traslado pero **por línea de expedición** (la demanda NO se agrupa por artículo). Devuelve `ExpedicionesAnalysisResult` (líneas + embudo).
- **`TransferFunnel.From(WeeklyAnalysisResult)`** — cuenta el embudo del flujo: Evaluados / Alcanzan / Para traer / Sin bodega externa. `ExpedicionesAnalysisResult` calcula su propio embudo equivalente.
- **`WeeklyAnalysisResultFilter.Apply(...)`** — filtra el resultado por texto + semáforo principal + semáforo satélite para vistas internas de análisis.

---

## 6. Capa de E/S de Excel (`PronosticosAbasto.Core/IO`)

Librería: **ClosedXML**. Cada parser **auto-detecta** su hoja y sus columnas (normalizando encabezados, sin acentos/mayúsculas), por lo que distintos formatos por empresa funcionan sin configurar nada.

| Parser → modelo | Detecta |
|---|---|
| `InventoryWorkbookParser` → `InventoryWorkbook` | Escanea **todas las hojas** y toma la primera con `Articulo` + zona (`Zona de almacenaje`/`Zona Almacenaje`) + `Cantidad unidades`. Captura `Descripcion` opcional. (Resuelve el caso Cofersa: hoja pivote primero, detalle después.) |
| `ForecastWorkbookParser` → `ForecastWorkbook` | Escanea ~12 filas buscando la columna de artículo (incl. `Item code`), detecta columnas de fecha **semanal** o de **mes** (`Jun 2026`), e infiere `ForecastGranularity` (Weekly/Monthly). EPA = semanal; Cofersa = mensual. |
| `ExpedicionesWorkbookParser` → `ExpedicionesWorkbook` | Soporta el reporte completo (`Pedidas`/`Preparadas` o cantidades mínimas) y archivos simplificados (`expedicion`/`articulo`/`descripcion`/`cantidad`). En el completo calcula el faltante como pedido - preparado, omite líneas completas y omite expediciones con `Situacion = ANUL`. |

- **`AnalysisWorkbookExporter`** — exporta a Excel. `Export(...)` genera el análisis completo (hojas Resumen / Resultado / Detalle por zonas). `ExportTransferRequisition(...)` genera la hoja **Requisicion** sólo con lo que hay que traer: Articulo · Descripcion · Disponible externas · Cantidad a traer · Zonas externas · Pendiente · Pedido.
- **`WorkbookValidationException`** — error de validación con mensaje legible para el operador.

---

## 7. La UI: una pantalla, cinco módulos

`MainPage.xaml` es una sola página con navegación lateral y módulos independientes:

1. **Traslados** (primaria) — el resultado del flujo: embudo (Evaluados/Alcanzan/Para traer/Sin bodega externa) + lista "Para traer de bodega externa" con `En externas`, `A traer`, desglose por zona (ordenado por cantidad, con tooltip) y `Pendiente`; checkbox **Pedido** (seguimiento) + Exportar requisición + Limpiar pedidos.
2. **Expediciones** — basada en `ReporteExpediciones.xlsx` o en un archivo simplificado de faltantes. La vista normal funciona como lista de excepciones: muestra solo líneas incompletas que no se cubren con OLO/principal y necesitan bodega externa o quedan pendientes. Incluye `Expedicion`, `Articulo`, `Descripcion`, `Faltante`, inventario y pallets.
3. **Transito** — seguimiento de pallets mandados a pedir y confirmación de llegada.
4. **Comparativa e histograma** — inventario OLO contra SERVICA, semanas que alcanza y distribución visual.
5. **Simulacion** — proyección de demanda y carga logística.

### Estado de vista
`MainPageViewModel` expone un bool por módulo visible (`ShowTransferView`, `ShowExpedicionesView`, `ShowTransitView`, `ShowWarehouseComparisonView`, `ShowSimulacionView`). Cada bool deriva una `Visibility` calculada.

### ViewModels de fila (proyección, no lógica)
`ArticleResultRowViewModel` (Tabla), `TransferRowViewModel` (Traslados, con `IsOrdered`), `ExpedicionRowViewModel` (Expediciones, inmutable), `ZoneDetailRowViewModel`, y los VMs de opciones (`WeekOptionViewModel`, `HorizonOptionViewModel`, `StatusFilterOptionViewModel`).

---

## 8. Estado por empresa y persistencia

- **`CompanyAnalysisSession`** (en memoria, una por empresa) guarda los workbooks cargados, la semana/horizonte/granularidad elegidos, el último análisis, los filtros, y el estado de Expediciones. Cambiar de empresa restaura su sesión sin recargar archivos.
- **Stores en disco** (JSON en `%LOCALAPPDATA%\...\PronosticosAbasto\`, redirigido por MSIX al `LocalCache` del paquete):
  - `SatelliteZoneStore` → `satellite-zones.json` (zonas externas por empresa).
  - `CoverageThresholdStore` → `coverage-thresholds.json` (umbrales del semáforo por empresa).
  - `TransferOrderStore` → `transfer-orders.json` (qué artículos están marcados como "Pedido", por empresa + periodo).
- **Configuración por empresa** se edita en el diálogo **Ajustes** (`SettingsDialogService`): umbral rojo %, umbral verde % y zonas externas.

---

## 9. Pipeline de datos (de archivo a pantalla)

```
Excel ─► [Parser auto-detecta hoja/columnas] ─► Workbook (modelo Core)
      ─► [Analyzer: zonas + horizonte + matemática de traslado] ─► AnalysisResult
      ─► [VM: BuildTransferView / BuildExpedicionesView / ApplyFilters] ─► Row VMs
      ─► [x:Bind a ListView virtualizada] ─► pantalla
```

Notas de rendimiento (medido con datos reales de Cofersa, ~34k entradas de forecast / ~16k de inventario):
- Parseo de Excel ≈ 2.7 s (E/S de ClosedXML, una sola vez al cargar).
- Análisis ≈ 47 ms; embudo ≈ 0 ms.
- Las listas grandes se pueblan por **asignación de un array** (una sola notificación) a una `ListView` **virtualizada**, no `Add` por fila.

---

## 10. Decisiones y convenciones clave

- **x:Bind con ComboBox:** nunca usar `SelectedValue` + `SelectedValuePath` en TwoWay (lanza NRE en el binding generado). Siempre `SelectedItem` enlazado a un VM de ítem.
- **Layout responsivo por code-behind:** `MainPage.xaml.cs` usa `SizeChanged` (no `AdaptiveTrigger`, que no disparaba dentro del grid anidado) para apilar el panel de detalle bajo la tabla en anchos < 900 px. La barra de controles va en un `ScrollViewer` horizontal.
- **Excepciones:** `App.xaml.cs` registra un manejador global que escribe a `crash.log` y marca `e.Handled = true` para que un error de UI no tumbe la app.
- **Despliegue MSIX (gotcha):** `dotnet build` actualiza binarios, pero los cambios de XAML/UI requieren `dotnet clean` + `dotnet build` + `dotnet run` para re-desplegar el layout; si no, el lanzamiento por AUMID sirve un layout viejo.
- **Caveat de zonas (importante):** si las zonas externas de una empresa no coinciden con las del inventario (p. ej. Cofersa guarda en zonas CLIRO pero el default es Guácima de EPA), el split principal/satélite sale **mal de forma silenciosa**. Hay que configurar las zonas reales en Ajustes antes de confiar en los números. (Mejora pendiente: validar/avisar — ver §12.)
- **Descripción y "En externas":** metadatos de display; no entran en ningún cálculo. El desglose por zona se muestra ordenado de mayor a menor (la zona con más stock primero = de dónde conviene traer).

---

## 11. Build y verificación

```bash
# Pruebas del Core (lógica)
dotnet test PronosticosAbasto.Core.Tests/PronosticosAbasto.Core.Tests.csproj

# UI (valida x:Bind en compilación). Tras cambios de XAML, limpiar primero:
dotnet clean PronosticosAbasto/PronosticosAbasto.csproj -c Debug
dotnet build PronosticosAbasto/PronosticosAbasto.csproj -c Debug
dotnet run   --project PronosticosAbasto/PronosticosAbasto.csproj -c Debug   # re-despliega y lanza
```

- **Pruebas:** 121 en `PronosticosAbasto.Core.Tests` (parsers, analizadores, embudo, exportador, clasificador, stores y motor de tablas). La lógica que mueve decisiones de compra se prueba a nivel Core.
- **Datos reales:** los parsers/analizadores se han validado contra los archivos reales de EPA y Cofersa (volúmenes de 16k–34k filas) mediante smoke tests temporales.

---

## 12. Limitaciones conocidas y backlog

- **Seguro de zonas:** falta avisar cuando las zonas configuradas no aparecen en el inventario cargado (hoy el split puede salir mal en silencio). *Prioridad alta.*
- **Rama "Sin bodega externa":** el embudo la cuenta pero no hay una lista accionable de "para comprar/escalar" con el faltante por artículo.
- **Expediciones "sin sumar":** si un artículo se repite en varias líneas, cada línea compara contra el inventario completo (posible doble conteo). Pendiente: toggle opcional "agrupar por artículo".
- **Persistencia de archivos:** la app no recuerda los últimos archivos cargados entre reinicios; no hay indicador de progreso durante la carga.
- **Empaquetado/entrega:** ya existe `tools/Create-WindowsInstaller.ps1` (genera el MSI en `dist/installer/`), pero falta versionado y firma para entregar a un usuario no técnico.
- **Cobertura de pruebas del ViewModel:** `MainPageViewModel` expone tipos de WinUI (`InfoBarSeverity`) y de LiveCharts (`ISeries`, `Axis`), así que probarlo exige un host de UI, no un proyecto xUnit normal. Su parte pura —el filtrado y ordenamiento de las cuatro tablas— ya salió al Core y sí está cubierta. Su constructor de 13 parámetros es la raíz de composición: lo usa el constructor sin parámetros, no hay inyección real mientras no exista ese host.

### Persistencia local y datos compartidos

Los stores de `Core/Storage` escriben JSON en `%LOCALAPPDATA%\PronosticosAbasto\`
mediante `LocalJsonStorage.WriteAtomic` (archivo temporal + `File.Move` con
reemplazo), así un corte a media escritura no deja un JSON truncado.

Sigue siendo almacenamiento **por máquina y por usuario**: dos operadores no
comparten marcas ni tránsito. Si eso llega a hacer falta, es un cambio de
backend, no un ajuste de los stores.
