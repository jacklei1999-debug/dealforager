# DealForager

An ASP.NET Core 8 MVC application for browsing deals, managing products and wish lists, and fetching deals in the background. It uses SQLite and SignalR notifications.

## Requirements

- .NET 8 SDK
- NuGet package restore (Entity Framework Core SQLite and Newtonsoft.Json).

The database models and deal-fetching implementation are included in `Data/`; no sibling project is required.

## Run locally

From the repository directory:

```sh
dotnet restore WebUIMVC.csproj
dotnet run --project WebUIMVC.csproj
```

Review the listening addresses in `Program.cs` and `appsettings.json` for your environment. The SQLite connection is configured in `appsettings.json`; the application ensures the database schema exists at startup. Local database files are excluded from Git.

Background fetch rules are configured in `fetchConfigs.json`. Restart the application after changing those rules.

## UI testing

`dotnet run --project WebUIMVC.csproj` uses the Development launch profile. In Development, background fetching runs for 60 seconds after the fetch workers start, then cancels all workers and in-flight requests. The web app stays running, and existing deals remain available for UI testing. Wishlist and other UI actions still work.

Change `Fetching:DurationSeconds` in `appsettings.Development.json` to adjust the window. Set it to `0` for continuous fetching. Each app restart starts a new fetch window. Production fetching remains continuous by default.
