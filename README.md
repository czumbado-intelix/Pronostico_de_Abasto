# Pronóstico de Abasto

App nativa de Windows (WinUI 3 / .NET 8) para abasto. Compara el **forecast de
demanda** contra el **inventario eFlow** y dice **qué hay que traer de las
bodegas satelitales** para cubrir lo que no alcanza la bodega principal, por
empresa (EPA, Cofersa).

El documento de diseño completo —reglas de negocio, paleta, modelo de dominio y
backlog— está en [DESIGN.md](DESIGN.md).

## Estructura

| Proyecto | Qué es |
|---|---|
| `PronosticosAbasto.Core` | Lógica pura, sin WinUI: análisis, parseo/exportación de Excel (ClosedXML), stores en disco y el motor de filtrado/orden de las tablas. |
| `PronosticosAbasto` | App WinUI 3: vistas, MVVM y diálogos. |
| `PronosticosAbasto.Core.Tests` | xUnit sobre el Core. |

## Requisitos

- .NET SDK 8 o superior
- Windows 10 1809 (10.0.17763) o superior

## Compilar y probar

```bash
dotnet test PronosticosAbasto.Core.Tests/PronosticosAbasto.Core.Tests.csproj
```

```bash
dotnet build PronosticosAbasto/PronosticosAbasto.csproj -c Debug
```

Tras cambiar XAML conviene limpiar primero, porque `x:Bind` se compila:

```bash
dotnet clean PronosticosAbasto/PronosticosAbasto.csproj -c Debug
```

Ejecutar la app (se re-despliega con identidad de paquete y se lanza):

```bash
dotnet run --project PronosticosAbasto/PronosticosAbasto.csproj -c Debug
```

## Instalador

```powershell
.\tools\Create-WindowsInstaller.ps1
```

Deja el MSI en `dist/installer/` (carpeta ignorada por git).

## Datos locales

La configuración y el estado operativo (marcas de "mandado a traer", tránsito,
zonas satélite, umbrales, comentarios) se guardan como JSON en
`%LOCALAPPDATA%\PronosticosAbasto\`. Es almacenamiento por usuario y por
máquina: no se comparte entre operadores.

Los errores no controlados quedan en `%LOCALAPPDATA%\PronosticosAbasto\crash.log`,
y la app avisa con un diálogo en vez de seguir en silencio.

## Tema

La app es de tema claro únicamente, siguiendo la guía visual del DESIGN.
