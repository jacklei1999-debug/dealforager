# Copilot Instructions for WebUIMVC

## Project Overview
This represents the UI and background processing layer of the DealForager system. It is an ASP.NET Core 8.0 MVC application that serves a web interface for viewing deals and runs background tasks to fetch new data.

## Architecture & Core Components

### 1. Structure
- **MVC Pattern**: Standard Controllers/Views structure.
  - `Controllers/`: Handle web requests. `HomeController` manages the main product feed.
  - `Views/`: Razor views for the UI.
  - `Services/`: Contains hosted services like `FetchBackgroundService`.
- **Data Layer**: `Data/` contains the EF Core `Context`, domain entities, fetch configuration, and deal fetcher in the `WebUIMVC.Data` namespace.
- **Database**: SQLite (`DealForagerDb`). The schema is automatically ensured/created at startup in `Startup.cs`.

### 2. Background Processing
- **FetchBackgroundService**: A `BackgroundService` that runs continuously.
- **Dynamic Configuration**: Driven by `fetchConfigs.json`. It loads a list of fetch rules (minimum savings, interval, category) and executes them.
  - *Pattern*: Configuration changes in this file require a restart to pick up (loaded on `ExecuteAsync` start).

## Key Workflows & Patterns

### Database & State Management
- **Product Lifecycle**: Products have a `readit` state field used in `HomeController`:
  - `<= 1`: Unseen. Loaded by Index, then immediately marked as `2`.
  - `2`: Seen/In-Queue.
  - `3`: Archived/Dismissed. Handled by `UpdateRead` action.
- **Connection Strings**: Defined in `appsettings.json`.
- **EF Core**: Uses `EnsureCreated()` pattern in `Startup.Configure` instead of migrations for simple deployment.

### Configuration & Environment
- **Hardcoded Bindings**: `Program.cs` uses `webBuilder.UseUrls()` with specific IPs (e.g., `192.168.201.6`).
  - *Action*: Be careful editing `Program.cs` host setup to avoid breaking local network access.
- **Custom Config**: `fetchConfigs.json` is copied to output (`CopyIfNewer`) and is critical for the background worker.

### Build & Publish
- Multiple publish profiles and target directories exist (`publish-mac`, `publish-windows`).
- The project builds independently with its NuGet package references.

## Development Guidelines
- **Logging**: Use `ILogger<T>` injected into constructors.
- **Async/Await**: Controllers and Services use async patterns for IO operations (DB updates, File reading).
- **JSON Serialization**: Use `System.Text.Json` with `PropertyNameCaseInsensitive = true`.
