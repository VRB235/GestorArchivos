# MediaVault & LinkHub

Aplicación de escritorio para Windows orientada a la gestión de enlaces web, indexación de archivos multimedia locales, etiquetado (categorías, actrices, productoras), estadísticas del sistema y notas rápidas.

Desarrollada con **.NET 8**, **C#**, **WPF**, **SQLite** y **LiveCharts2**, siguiendo el patrón **MVVM limpio**.

---

## Módulos principales

La aplicación incluye un **menú lateral fijo (Sidebar)** para navegar entre los módulos:

| Módulo | Descripción |
|--------|-------------|
| **Dashboard & Estadísticas** | KPIs (archivos, aperturas, sin abrir/sin ranking), gráficos LiveCharts2 colapsables, recomendaciones regenerables (mixta y por estrellas). |
| **Link Manager** | CRUD de enlaces web, logos locales, productoras, apertura **siempre en Firefox** (ventana normal). |
| **File & Media Vault** | Explorador de carpetas/archivos, rankings, categorías/actrices/productoras por video, mover archivos, miniaturas. |
| **Categorías** | Catálogo de categorías de video (asignación desde Media Vault). |
| **Actrices** | CRUD de actrices y búsqueda de videos con filtros OR/AND (actrices, categorías, productoras); doble clic abre el video. |
| **Productoras** | Catálogo de productoras/fuentes (asociables a videos y a enlaces). |
| **Scratchpad** | Notas rápidas de texto con operaciones CRUD simples. |
| **Configuración** | Ruta raíz de indexación y limpieza de metadatos multimedia. |

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
    │       ├── VideoCategory.cs
    │       ├── Actress.cs
    │       ├── Producer.cs
    │       └── QuickNote.cs
    │
    ├── MediaVault.LinkHub.Application/      ← Contratos de servicio y DTOs
    │   ├── Services/
    │   │   ├── IWebLinkService.cs
    │   │   ├── IMediaVaultService.cs
    │   │   ├── IVideoCategoryService.cs
    │   │   ├── IActressService.cs
    │   │   ├── IProducerService.cs
    │   │   ├── IDashboardService.cs
    │   │   └── IQuickNoteService.cs
    │   └── Models/
    │       ├── Dashboard/
    │       └── MediaVault/
    │
    ├── MediaVault.LinkHub.Infrastructure/   ← EF Core, SQLite, servicios
    │   ├── Configurations/
    │   ├── Data/                            ← DbContext, migraciones
    │   ├── Launchers/                       ← Firefox, VLC, apertura nativa
    │   ├── Media/                           ← Extensiones, logos, VLC path
    │   ├── Services/                        ← Implementaciones CRUD
    │   └── DependencyInjection.cs
    │
    └── MediaVault.LinkHub.App/              ← WPF + MVVM
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml                  ← Shell con Sidebar
        ├── Navigation/                      ← INavigationService
        ├── ViewModels/                      ← ViewModels por módulo
        ├── Views/                           ← Vistas XAML
        ├── Shell/                           ← Miniaturas, Pictures, diálogos
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

| ViewModel | Servicio(s) principal(es) |
|-----------|---------------------------|
| `DashboardViewModel` | `IDashboardService`, `IMediaVaultService` |
| `LinkManagerViewModel` | `IWebLinkService`, `IProducerService` |
| `MediaVaultViewModel` | `IMediaVaultService`, categorías/actrices/productoras |
| `VideoCategoryManagerViewModel` | `IVideoCategoryService` |
| `ActressesViewModel` | `IActressService`, filtros + apertura de videos |
| `ProducerManagerViewModel` | `IProducerService` |
| `ScratchpadViewModel` | `IQuickNoteService` |
| `SettingsViewModel` | `IAppSettingsService`, `IMediaVaultService` |

---

## Entidades de dominio

### `WebLink` — Link Manager

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Id` | `int` | Identificador autogenerado |
| `Nombre` | `string` | Nombre descriptivo del enlace |
| `Url` | `string` | URL destino (única en BD) |
| `LogoPath` | `string?` | Ruta al logo en el almacén de la app (`%LocalAppData%\...\WebLinkLogos\`); se copia al guardar |
| `Categoria` | `LinkCategory` | `Oficial`, `Descarga`, `Gratis` |
| `FechaUltimaActualizacion` | `DateTime?` | Visita/revisión marcada por el usuario |
| `Producers` | M:N | Productoras asociadas al sitio |

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
| `Categories` | M:N | Categorías de video |
| `Actresses` | M:N | Actrices asociadas |
| `Producers` | M:N | Productoras asociadas |

> `RankingGlobal` es `[NotMapped]`. Para LINQ traducible a SQL:

```csharp
MediaFile.ComputeRankingGlobal(file)
// o: (f.RankingCalidad + f.RankingContenido + f.RankingGusto) / 3.0
```

### `VideoCategory` / `Actress` / `Producer`

Catálogos con `Name`, `SortOrder` y relaciones M:N con `MediaFile`. `Producer` también se relaciona con `WebLink`.

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

| Tabla / unión | Índices / notas |
|---------------|-----------------|
| `WebLinks` | `Url` (único), `Categoria` |
| `MediaFiles` | `Path` (único), `VecesAbierto`, `Extension` |
| `VideoCategories` | nombre |
| `Actresses` / `Producers` | nombre |
| `MediaFileVideoCategory`, `MediaFileActresses`, `MediaFileProducers` | M:N videos |
| `WebLinkProducers` | M:N enlaces ↔ productoras |
| `QuickNotes` | `FechaCreacion` |

---

## Servicios de aplicación

| Interfaz | Implementación | Destacado |
|----------|----------------|-----------|
| `IWebLinkService` | `WebLinkService` | CRUD + logos + productoras + `OpenInBrowserAsync` (Firefox) |
| `IMediaVaultService` | `MediaVaultService` | Indexación, rankings, mover archivos, etiquetas, VLC |
| `IVideoCategoryService` | `VideoCategoryService` | CRUD de categorías |
| `IActressService` | `ActressService` | CRUD + `FindVideosByFiltersAsync` (OR/AND) |
| `IProducerService` | `ProducerService` | CRUD de productoras |
| `IDashboardService` | `DashboardService` | KPIs, tops, distribución, recomendaciones |
| `IQuickNoteService` | `QuickNoteService` | CRUD de notas |

### Lanzamiento de procesos (`Process.Start`)

| Componente | Comportamiento |
|------------|----------------|
| `BrowserLauncher` | Abre la URL en **Firefox** (ventana normal). Busca en rutas típicas y en el registro de Mozilla; si no hay Firefox, cae al shell. |
| `MediaFileLauncher` | Apertura nativa con `UseShellExecute` o forzando **VLC** si `preferVlc = true` |

---

## Miniaturas y carpeta `Pictures`

Al iniciar la sesión, por cada carpeta de medios se elige **una imagen al azar** de `{carpeta}/Pictures` y se reutiliza durante el proceso (Dashboard, Actrices, explorador del Vault).

Prioridad de miniatura de carpeta: **Pictures (sesión)** → icono personalizado → miniatura de Shell.

Estructura esperada:

```text
CarpetaDelContenido/
  Pictures/
    img1.jpg
    img2.png
  video.mp4
```

Extensiones admitidas: `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp`, `.gif`.

---

## Dashboard — LiveCharts2

### Paquete

```
LiveChartsCore.SkiaSharpView.WPF 2.0.5
```

### Capacidades

- KPIs: totales, aperturas de video, videos nunca abiertos, videos sin ranking.
- Gráficos colapsables (Top vistas, distribución por categoría, rankings).
- Recomendación mixta ponderada (regenerable).
- Recomendación por tiers de estrellas (5→1) con exclusión de sesión.
- Vista previa al pasar el mouse sobre series de archivos.

### Flujo de datos

```
IDashboardService.GetStatisticsAsync()
        ↓
DashboardViewModel
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

Si no hay datos, el Dashboard muestra mensajes orientativos en lugar de gráficos vacíos.

---

## Requisitos previos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 (WPF)
- **Firefox** recomendado para Link Manager (apertura de enlaces)
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
| **Servicios** | `QuickNoteService`, `VideoCategoryService`, `ActressService`, `WebLinkService`, `DashboardService`, `MediaVaultService` |
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

> En **Debug**, el gate de seguridad no bloquea el arranque. En **Release**, aplica el flujo de acceso configurado.

### Flujo recomendado para probar

1. **Configuración** → definir carpeta raíz de indexación.
2. **Media Vault** → explorar / indexar; asignar categorías, actrices y productoras; abrir videos para incrementar `VecesAbierto`.
3. Opcional: colocar imágenes en `{carpeta}/Pictures` y reiniciar la app para ver miniaturas de sesión.
4. **Actrices** → filtrar por actrices/categorías/productoras; doble clic para abrir.
5. **Link Manager** → crear enlaces, asociar productoras; **Abrir en Firefox**.
6. **Dashboard** → **Actualizar** / regenerar recomendaciones.

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
| Relaciones M:N (categorías/actrices/productoras) | Etiquetado flexible sin duplicar archivos |
| `IDbContextFactory<AppDbContext>` | Contexto por operación; seguro en WPF |
| Servicios **Transient** | Compatible con contenedor DI raíz de WPF |
| Datos Debug vs Release aislados | Evitar contaminar datos de producción al desarrollar |
| `BrowserLauncher` → Firefox fijo | Comportamiento predecible al abrir enlaces |
| Miniaturas `Pictures` cacheadas por proceso | Variedad al reiniciar sin costar I/O en cada vista |
| `DashboardChartFactory` separado | ViewModel limpio; series reutilizables |
| Tema oscuro en `Styles.xaml` | Coherencia visual con gráficos SkiaSharp |

---

## Estado actual del proyecto

| Capacidad | Estado |
|-----------|--------|
| Domain / Application / Infrastructure / WPF | ✅ |
| Dashboard (KPIs, charts, recomendaciones) | ✅ |
| Link Manager (CRUD, logos, productoras, Firefox) | ✅ |
| Media Vault (indexación, rankings, mover, etiquetas) | ✅ |
| Categorías / Actrices / Productoras | ✅ |
| Miniaturas desde `Pictures` (sesión) | ✅ |
| Aislamiento datos Debug/Release | ✅ |
| Publicación autocontenida (win-x64) | ✅ |
| Confirmación antes de eliminar | ✅ |
| Tests unitarios (servicios + gráficos) | ✅ |

---

## Notas

- Las advertencias **NU1701** al compilar (OpenTK / SkiaSharp.Views.WPF) son dependencias transitivas de LiveCharts2 y no impiden la ejecución.
- VLC es opcional; si no está instalado, la apertura usa el visor predeterminado de Windows.
- Si Firefox no está instalado, la apertura de enlaces intenta el manejador de URL del sistema como respaldo.

---

## Licencia

Proyecto personal — todos los derechos reservados.
