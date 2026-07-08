# Goat Records — ASP.NET Core 9 MVC

Production-ready farm management app migrated from the static `goat-records-v7.html` demo.

## Solution structure

```
GoatFarm.sln
src/
  GoatFarm.Domain/          — Entities, enums, constants
  GoatFarm.Application/     — Service interfaces, ViewModels, validation
  GoatFarm.Infrastructure/  — EF Core, repositories, services, seed data
  GoatFarm.Web/             — MVC controllers, Razor views, CSS, JavaScript
```

## Prerequisites

- .NET 9 SDK
- SQL Server or LocalDB (default connection uses LocalDB)

## Setup

1. Update the connection string in `src/GoatFarm.Web/appsettings.json` if needed.
2. Apply migrations and run:

```bash
cd "d:\Code\My Products\Goat Records"
dotnet ef database update --project src/GoatFarm.Infrastructure --startup-project src/GoatFarm.Web
dotnet run --project src/GoatFarm.Web
```

3. Open `https://localhost:5001` or the URL shown in the console.

On first run, the database is migrated and seeded with demo data matching the original HTML app.

## Features

- **Herd** — goats, groups, filters, bulk move
- **Feed & cost** — prices, rations, monthly summary, buying list
- **Milk** — collection and sales
- **Finance** — capital, income, expenses, profit
- **Vaccines** — schedules, due/upcoming, reminders, history

All business logic runs in C# services; the UI uses the original CSS and layout with AJAX CRUD.

## Authentication

ASP.NET Identity is configured with roles **Admin**, **Manager**, and **Staff**. Login UI is stubbed at `/Account/Login` for future implementation.

## Regenerate migrations

```bash
dotnet ef migrations add MigrationName --project src/GoatFarm.Infrastructure --startup-project src/GoatFarm.Web
```
