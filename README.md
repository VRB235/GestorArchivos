# MediaVault & LinkHub

Aplicación de escritorio para Windows orientada a la gestión de enlaces web, indexación de archivos multimedia locales, estadísticas del sistema y notas rápidas.

Desarrollada con **.NET 8**, **C#**, **WPF**, **SQLite** y **LiveCharts2**, siguiendo el patrón **MVVM limpio**.

---

## Módulos principales

La aplicación incluye un **menú lateral fijo (Sidebar)** para navegar entre cuatro módulos:

| Módulo | Descripción |
|--------|-------------|
| **Dashboard & Estadísticas** | KPIs y gráficos LiveCharts2 (Top 10, distribución de enlaces, ranking promedio). |
| **Link Manager** | CRUD de enlaces web con apertura en navegador predeterminado (modo incógnito). |
| **File & Media Vault** | Explorador recursivo que indexa carpetas y archivos (imágenes y videos). |
| **Scratchpad** | Notas rápidas de texto con operaciones CRUD simples. |

---

## Stack tecnológico

| Tecnología | Uso |
|------------|-----|
| **.NET 8** / C# | Runtime y lenguaje |
| **WPF** | Interfaz de escritorio |
| **CommunityToolkit.Mvvm** | ViewModels, comandos y binding |
| **Entity Framework Core 8** + **SQLite** | Persistencia local |
| **LiveChartsCore.SkiaSharpView.WPF 2.0.5** | Gráficos en el Dashboard |
| **Microsoft.Extensions.DependencyInjection** | Inyección de dependencias |

---

## Estructura de la solución

```
MediaVault.LinkHub.slnx
└── src/
    ├── MediaVault.LinkHub.Domain/           ← Entidades y enums
    │   ├── Common/EntityBase.cs
    │   ├── Enums/LinkCategory.cs
    │   └── Entities/
    │       ├── WebLink.cs
    │       ├── MediaFile.cs
    │       └── QuickNote.cs
    │
    ├── MediaVault.LinkHub.Application/      ← Contratos de servicio y DTOs
    │   ├── Services/
    │   │   ├── IWebLinkService.cs
    │   │   ├── IMediaVaultService.cs
    │   │   ├── IDashboardService.cs
    │   │   └── IQuickNoteService.cs
    │   └── Models/
    │       ├── Dashboard/
    │       └── MediaVault/
    │
    ├── MediaVault.LinkHub.Infrastructure/   ← EF Core, SQLite, servicios
    │   ├── Configurations/
    │   ├── Data/                            ← DbContext, migraciones
    │   ├── Launchers/                       ← Process.Start (navegador, VLC)
    │   ├── Media/                           ← Extensiones soportadas
    │   ├── Services/                        ← Implementaciones CRUD
    │   └── DependencyInjection.cs
    │
    └── MediaVault.LinkHub.App/              ← WPF + MVVM
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml                  ← Shell con Sidebar
        ├── Navigation/                      ← INavigationService
        ├── ViewModels/                      ← ViewModels por módulo
        ├── Views/                           ← Vistas XAML
        ├── Charts/                          ← DashboardChartFactory (LiveCharts2)
        ├── Resources/Styles.xaml            ← Tema oscuro
        └── Converters/
tests/
└── MediaVault.LinkHub.Tests/                ← xUnit (servicios + DashboardChartFactory)
```

---

## Arquitectura MVVM

```
┌─────────────────────────────────────────────────────────┐
│  Views (XAML)          ← DataTemplates por ViewModel    │
├─────────────────────────────────────────────────────────┤
│  ViewModels            ← CommunityToolkit.Mvvm          │
├─────────────────────────────────────────────────────────┤
│  Application           ← Interfaces + DTOs              │
├─────────────────────────────────────────────────────────┤
│  Infrastructure        ← Servicios + EF Core + SQLite   │
├─────────────────────────────────────────────────────────┤
│  Domain                ← Entidades puras                │
└─────────────────────────────────────────────────────────┘
```

### Navegación

- `INavigationService` resuelve ViewModels vía DI y dispara `InitializeAsync` al cambiar de módulo.
- `MainWindow` enlaza un `ContentControl` al `CurrentViewModel` con DataTemplates automáticos.

### ViewModels

| ViewModel | Servicio inyectado |
|-----------|-------------------|
| `DashboardViewModel` | `IDashboardService` |
| `LinkManagerViewModel` | `IWebLinkService` |
| `MediaVaultViewModel` | `IMediaVaultService` |
| `ScratchpadViewModel` | `IQuickNoteService` |

---

## Entidades de dominio

### `WebLink` — Link Manager

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Id` | `int` | Identificador autogenerado |
| `Nombre` | `string` | Nombre descriptivo del enlace |
| `Url` | `string` | URL destino (única en BD) |
| `LogoPath` | `string?` | Ruta local al logo/icono |
| `Categoria` | `LinkCategory` | `Oficial`, `Descarga`, `Gratis` |

### `MediaFile` — File & Media Vault

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Id` | `int` | Identificador autogenerado |
| `Path` | `string` | Ruta absoluta en disco (única) |
| `Name` | `string` | Nombre del archivo |
| `Extension` | `string` | Extensión (`.jpg`, `.mp4`, etc.) |
| `VecesAbierto` | `int` | Contador de aperturas |
| `RankingCalidad` | `double` | Estrellas 0–5 |
| `RankingContenido` | `double` | Estrellas 0–5 |
| `RankingGusto` | `double` | Estrellas 0–5 |
| `RankingGlobal` | `double` | **Calculado** — promedio de los 3 anteriores (escala 0–5) |

> `RankingGlobal` es `[NotMapped]`. Para LINQ traducible a SQL:

```csharp
MediaFile.ComputeRankingGlobal(file)
// o: (f.RankingCalidad + f.RankingContenido + f.RankingGusto) / 3.0
```

### `QuickNote` — Scratchpad

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Id` | `int` | Identificador autogenerado |
| `Contenido` | `string` | Texto (máx. 4000 caracteres) |
| `FechaCreacion` | `DateTime` | Fecha/hora UTC |

---

## Esquema SQLite

**Ubicación por ambiente** (Debug y Release no comparten datos):

| Ambiente | Carpeta | Cuándo |
|----------|---------|--------|
| **Production** | `%LocalAppData%\MediaVaultLinkHub\` | `Release` / `dotnet publish` |
| **Development** | `%LocalAppData%\MediaVaultLinkHub.Development\` | `Debug` / F5 |

Base de datos: `mediavault_linkhub.db` (mismo nombre en ambas carpetas).

Opcional: forzar ambiente con la variable `MEDIAVAULT_ENVIRONMENT=Development|Production`.

| Tabla | Índices relevantes |
|-------|-------------------|
| `WebLinks` | `Url` (único), `Categoria` |
| `MediaFiles` | `Path` (único), `VecesAbierto`, `Extension` |
| `QuickNotes` | `FechaCreacion` |

---

## Servicios de aplicación

| Interfaz | Implementación | Destacado |
|----------|----------------|-----------|
| `IWebLinkService` | `WebLinkService` | CRUD + `OpenInBrowserAsync` (incógnito) |
| `IMediaVaultService` | `MediaVaultService` | Indexación recursiva, rename, VLC |
| `IDashboardService` | `DashboardService` | Top 10, distribución, promedio rankings |
| `IQuickNoteService` | `QuickNoteService` | CRUD de notas |

### Lanzamiento de procesos (`Process.Start`)

| Componente | Comportamiento |
|------------|----------------|
| `BrowserLauncher` | Detecta navegador predeterminado vía registro Windows; flags `--inprivate`, `--incognito`, `-private-window` según motor; fallback a rutas conocidas |
| `MediaFileLauncher` | Apertura nativa con `UseShellExecute` o forzando **VLC** si `preferVlc = true` |

---

## Dashboard — LiveCharts2

### Paquete

```
LiveChartsCore.SkiaSharpView.WPF 2.0.5
```

### Gráficos implementados

| Gráfico | Control | Serie | Fuente de datos |
|---------|---------|-------|-----------------|
| Top 10 más vistos | `CartesianChart` | `RowSeries<int>` | `MediaFileViewStats.VecesAbierto` |
| Enlaces por categoría | `PieChart` | `PieSeries<int>` | `CategoryDistributionItem` |
| Ranking global | `CartesianChart` | `ColumnSeries<double>` | Promedio 0–5 estrellas del sistema |

### Flujo de datos

```
IDashboardService.GetStatisticsAsync()
        ↓
DashboardViewModel.UpdateCharts()
        ↓
DashboardChartFactory  →  ObservableCollection<ISeries>
        ↓
DashboardView.xaml     →  CartesianChart / PieChart
```

### Configuración al arranque (`App.xaml.cs`)

```csharp
LiveCharts.Configure(config =>
    config.AddSkiaSharp().AddDefaultMappers());
```

### Estados vacíos

Si no hay datos, el Dashboard muestra mensajes orientativos en lugar de gráficos vacíos (`HasTopViewsData`, `HasCategoryData`).

---

## Requisitos previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (WPF + registro de navegador predeterminado)
- [EF Core CLI](https://learn.microsoft.com/ef/core/cli/dotnet) (solo para migraciones):

```bash
dotnet tool install --global dotnet-ef
```

---

## Compilación

```bash
dotnet build MediaVault.LinkHub.slnx
```

---

## Pruebas unitarias

Proyecto `tests/MediaVault.LinkHub.Tests` (xUnit + FluentAssertions + SQLite en memoria):

```bash
dotnet test MediaVault.LinkHub.slnx -c Release
```

| Área | Cobertura principal |
|------|---------------------|
| **Servicios** | `QuickNoteService`, `VideoCategoryService`, `WebLinkService`, `DashboardService`, `MediaVaultService` |
| **Gráficos** | `DashboardChartFactory` (series, ejes, filtros de datos vacíos) |

---

## Publicación (ejecutable autocontenido)

Genera un paquete **win-x64** que incluye el runtime .NET 8; no requiere SDK ni runtime instalado en el equipo destino (solo Windows 10/11 x64).

```bash
dotnet publish src/MediaVault.LinkHub.App/MediaVault.LinkHub.App.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o publish/win-x64
```

Perfil equivalente (Visual Studio o CLI):

```bash
dotnet publish src/MediaVault.LinkHub.App/MediaVault.LinkHub.App.csproj ^
  -c Release ^
  -p:PublishProfile=win-x64-self-contained
```

Salida: carpeta `publish/win-x64/` con el ejecutable `MediaVault.LinkHub.App.exe` y todas las dependencias (~190 MB). Copie la carpeta completa para distribuir la aplicación.

> **Nota:** no use `PublishTrimmed` en WPF; puede romper reflexión y recursos XAML. VLC sigue siendo externo (opcional) en el equipo destino.

---

## Ejecutar la aplicación

```bash
dotnet run --project src/MediaVault.LinkHub.App/MediaVault.LinkHub.App.csproj
```

Al iniciar, la aplicación:

1. Configura LiveCharts2 (SkiaSharp).
2. Registra servicios vía DI (`AddMediaVaultLinkHubInfrastructure` + `AddPresentation`).
3. Aplica migraciones SQLite pendientes.
4. Abre `MainWindow` con el Dashboard como vista inicial.

### Flujo recomendado para probar el Dashboard

1. **Media Vault** → seleccionar carpeta → **Indexar** archivos multimedia.
2. Abrir archivos (botón **Abrir** o **VLC**) para incrementar `VecesAbierto`.
3. **Link Manager** → crear enlaces en distintas categorías.
4. **Dashboard** → **Actualizar** para ver los gráficos.

---

## Inyección de dependencias

### Registro (App.xaml.cs)

```csharp
var services = new ServiceCollection();
services.AddMediaVaultLinkHubInfrastructure();
services.AddPresentation();

Services = services.BuildServiceProvider();
await Services.InitializeDatabaseAsync();
```

### Uso programático (sin UI)

```csharp
using MediaVault.LinkHub.Infrastructure;
using MediaVault.LinkHub.Application.Services;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMediaVaultLinkHubInfrastructure();
var provider = services.BuildServiceProvider();
await provider.InitializeDatabaseAsync();

var linkService = provider.GetRequiredService<IWebLinkService>();
await linkService.OpenInBrowserAsync(linkId);
```

### Base de datos — conexión personalizada

```csharp
services.AddMediaVaultLinkHubInfrastructure(@"C:\ruta\personalizada\mi_base.db");
```

---

## Migraciones EF Core

### Crear migración

```bash
dotnet ef migrations add <NombreMigracion> ^
  --project src/MediaVault.LinkHub.Infrastructure/MediaVault.LinkHub.Infrastructure.csproj ^
  --output-dir Data/Migrations
```

### Aplicar migraciones

```bash
dotnet ef database update ^
  --project src/MediaVault.LinkHub.Infrastructure/MediaVault.LinkHub.Infrastructure.csproj
```

### Revertir última migración

```bash
dotnet ef migrations remove ^
  --project src/MediaVault.LinkHub.Infrastructure/MediaVault.LinkHub.Infrastructure.csproj
```

---

## Decisiones técnicas

| Decisión | Motivo |
|----------|--------|
| `EntityBase` con `Id` común | Homogeneidad entre entidades |
| `LinkCategory` como `string` en SQLite | Legibilidad en herramientas de BD |
| `IDbContextFactory<AppDbContext>` | Contexto por operación; seguro en WPF |
| Servicios **Transient** | Compatible con contenedor DI raíz de WPF |
| `DashboardChartFactory` separado | ViewModel limpio; series reutilizables |
| `net8.0-windows` en Infrastructure | Registro Windows para navegador incógnito |
| Tema oscuro en `Styles.xaml` | Coherencia visual con gráficos SkiaSharp |

---

## Estado actual del proyecto

| Capa | Estado |
|------|--------|
| Domain (entidades, enums) | ✅ Completado |
| Application (interfaces, DTOs) | ✅ Completado |
| Infrastructure (EF Core, servicios, launchers) | ✅ Completado |
| WPF App (MVVM, Sidebar, Views) | ✅ Completado |
| Dashboard (LiveCharts2) | ✅ Completado |
| Publicación autocontenida (win-x64) | ✅ Completado |
| Confirmación antes de eliminar (archivos/enlaces) | ✅ Completado |
| Búsqueda y filtrado en Media Vault | ✅ Completado |
| Tests unitarios (servicios + gráficos) | ✅ Completado |

---

## Posibles mejoras futuras


---

## Notas

- Las advertencias **NU1701** al compilar (OpenTK / SkiaSharp.Views.WPF) son dependencias transitivas de LiveCharts2 y no impiden la ejecución.
- VLC es opcional; si no está instalado, la apertura usa el visor predeterminado de Windows.

---

## Licencia

Proyecto personal — todos los derechos reservados.
