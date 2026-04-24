# Copilot Instructions for LobbyService

## Build, test, and lint

### Build
- Restore/build the solution from the repo root: `dotnet build LobbyService.sln`
- Run the API directly: `dotnet run --project LobbyService\LobbyService.csproj`

### Test
- Run the solution tests: `dotnet test LobbyService.sln`
- There is currently no dedicated test project in the repository, so there is no repo-specific single-test command yet.

### Lint
- There is no separate lint or formatting command configured in the repository.

## High-level architecture

- This repository contains a single ASP.NET Core Web API project targeting **.NET 8** (`LobbyService\LobbyService.csproj`).
- `LobbyService\Program.cs` uses the top-level host/bootstrap pattern. It registers MVC controllers with `AddControllers()`, enables OpenAPI discovery with `AddEndpointsApiExplorer()`, and adds Swagger generation with `AddSwaggerGen()`.
- The HTTP surface is controller-based, not minimal APIs. Requests are exposed through classes in `LobbyService\Controllers\` and wired by `app.MapControllers()`.
- Swagger middleware is only enabled when `app.Environment.IsDevelopment()` is true, so local development expects the Swagger UI but non-development environments do not automatically expose it.
- The current API is the template `WeatherForecastController`, which serves `GET /weatherforecast` and returns `WeatherForecast` objects from `LobbyService\WeatherForecast.cs`.
- `Properties\launchSettings.json` defines the main local development profile. The `http` profile binds to `http://localhost:5048` and opens the Swagger UI at `/swagger`.
- `appsettings.json` and `appsettings.Development.json` currently only configure logging and host settings; there is no custom application configuration structure yet.

## Key conventions

- Keep startup and middleware wiring in `Program.cs`; this project does not use a separate `Startup` class.
- Add new HTTP endpoints as MVC controllers under `LobbyService\Controllers\`, using attribute routing and `[ApiController]` like the existing controller.
- The current route style relies on the controller token pattern (`[Route("[controller]")]`), so controller class names directly shape endpoint paths unless you intentionally override them.
- Models currently live in the root project namespace (`namespace LobbyService`) while controllers use `namespace LobbyService.Controllers`; follow that convention unless the project is intentionally reorganized.
- Preserve the Development-only Swagger behavior unless a change explicitly requires API docs outside local development.
- The solution and project both define `Any CPU` and `x64` configurations/platforms. Keep new project settings compatible with those existing solution configurations.
