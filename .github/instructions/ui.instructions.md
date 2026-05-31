---
applyTo: "UI/**/*.cs,UI/**/*.xaml,UI/**/*.razor"
---

# UI & orchestration (`UI/*`)

| Path | Stack | Role |
|------|-------|------|
| `UI/WoWStateManagerUI` | WPF (.NET 8) | desktop state-monitoring UI |
| `UI/StorylineManager` | ASP.NET | quest/storyline authoring + progression UI |
| `UI/Systems/Systems.AppHost` | .NET Aspire | service orchestration (AppHost) |
| `UI/Systems/Systems.ServiceDefaults` | Aspire | shared service defaults |

(`UI/StateManagerUI`, `UI/WWoW.Systems`, `UI/Bot` are legacy — prefer the
current projects above.)

## Conventions

- **WPF:** keep the UI thread free — long/IO work goes on background tasks,
  marshal back with the dispatcher. Use the existing `Converters/` for binding
  conversions; follow the established MVVM-ish binding pattern.
- **FG interaction:** any UI that triggers ForegroundBotRunner work must be
  **state-gated** and must **never steal focus or capture the cursor**.
- **Aspire (`Systems.AppHost`):** wire services/ports here; don't hardcode
  endpoints in consumers when the AppHost can inject them.

## Validate with

```powershell
.\scripts\build.ps1
dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release   # if touching ServiceDefaults
```

## See also

- `UI/CLAUDE.md`; service/port map in [`services.instructions.md`](services.instructions.md).
