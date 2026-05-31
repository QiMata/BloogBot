---
name: wpf-dashboard-panel
description: Extend the WPF operator console (WoWStateManagerUI) with a new panel/view bound to a StateManager summary, following MVVM + UI-thread + state-gating rules. Use when adding a UI component or page to the desktop dashboard.
trigger: add a UI panel, new dashboard view, WPF page, operator console, StateManager summary panel, MVVM view model, add a view to WoWStateManagerUI
---

# WPF Dashboard Panel

## Goal

Add one panel/view to the `WoWStateManagerUI` desktop console (e.g. a new tab on
the dashboard) that displays/controls StateManager data through an MVVM ViewModel —
without blocking the UI thread, stealing focus, or capturing the cursor.

## Inputs

- What the panel shows/controls and which StateManager summary data it binds to.
- Key files (all verified):
  - Views: `UI/WoWStateManagerUI/Views/*.xaml` (+ `.xaml.cs`) — e.g.
    `DashboardView`, `ServicesView`, `ServiceManagementView`,
    `AccountManagementView`, `MangosConsoleView`; hosted by
    `UI/WoWStateManagerUI/MainWindow.xaml`.
  - ViewModels: `UI/WoWStateManagerUI/ViewModels/*.cs` — e.g. `MainViewModel`,
    `DashboardViewModel`, `BotSnapshotViewModel`, `DashboardActivityViewModel`,
    `DashboardCharacterViewModel`, `ServicesViewModel`.
  - Data services: `UI/WoWStateManagerUI/Services/*.cs` — `UIListenerService`
    (receives StateManager updates), `HealthCheckService`,
    `ActivityCatalogService`, `WorldDataService`.
  - Binding helpers: `UI/WoWStateManagerUI/Converters/*.cs`,
    `Handlers/{AsyncCommandHandler,CommandHandler}.cs`,
    `Behaviors/ComboBoxBehaviors.cs`, `Themes/WoWTheme.xaml`.
  - DI/bootstrap: `UI/WoWStateManagerUI/App.xaml.cs`.
  - StateManager source of the summary: `Services/WoWStateManager/`.
- Area rules: `.github/instructions/ui.instructions.md`; `UI/CLAUDE.md`.

## Preconditions

- The data the panel needs is available from StateManager (a summary API/snapshot)
  — if not, add it on the StateManager side first (see [[docker-stack-extension]] /
  [[metrics-instrumentation]] for service-side surfaces).
- The UI builds: `.\scripts\build.ps1`.
- Ports/endpoints are read from the Aspire wiring, not hardcoded in the UI.

## Procedure

1. **Add the View**: create `UI/WoWStateManagerUI/Views/<Name>View.xaml` (+
   `.xaml.cs`), styled via `Themes/WoWTheme.xaml`; copy an existing view
   (`ServicesView`/`DashboardView`) for layout + binding conventions.
2. **Add the ViewModel**: `UI/WoWStateManagerUI/ViewModels/<Name>ViewModel.cs`
   implementing `INotifyPropertyChanged` (match the existing ViewModels); expose
   bound properties and `ICommand`s via `AsyncCommandHandler`/`CommandHandler`.
3. **Bind to data**: consume `UIListenerService` (StateManager updates) /
   `HealthCheckService` etc.; do **all** I/O on background tasks and marshal
   property updates back with `Dispatcher.Invoke`/`InvokeAsync` so the UI thread
   stays free.
4. **Register** the View/ViewModel in DI (`App.xaml.cs`) and surface it from
   `MainWindow.xaml`/`MainViewModel` (new tab or nav entry).
5. **Add converters** in `Converters/` for any value→visual transforms (follow
   `ServiceStatusToBrushConverter`/`BoolToVisibilityConverter`).
6. **State-gate any FG-affecting control**: a button that triggers foreground/
   client action must be disabled unless the relevant state allows it, and must
   never steal focus or capture the cursor.

## Verification

- Build: `.\scripts\build.ps1`.
- UI tests: `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release`.
- Run the console and confirm: the panel renders, updates live from StateManager,
  the UI stays responsive under load, and FG-affecting controls are correctly
  gated.

## Outputs

- New `Views/<Name>View.xaml(.cs)` + `ViewModels/<Name>ViewModel.cs`
  (+ any `Converters/`), DI registration, and a nav/tab entry.
- A UI test covering the ViewModel logic.
- Doc note if a new StateManager summary surface was added (AGENTS.md §9).

## Failure modes and recovery

- **Blocking the UI thread.** Synchronous I/O in the ViewModel freezes the app —
  push work to a background task and marshal back via the dispatcher.
- **Stealing focus / capturing the cursor** from an FG-triggering control — both
  are forbidden; keep controls state-gated and passive.
- **Hardcoding service ports** in the UI instead of using the AppHost wiring.
- **Logic in code-behind.** Keep behavior in the ViewModel (MVVM); code-behind is
  for view wiring only.
- **Extending legacy UI** (`StateManagerUI/`, `WWoW.Systems/`, `Bot/`) — do not;
  build in `WoWStateManagerUI`.

## Related skills

- [[docker-stack-extension]] — add the service whose data the panel shows.
- [[metrics-instrumentation]] — expose the summary numbers the panel binds to.
- [[activity-catalog-bootstrap]] — the catalog `ActivityCatalogService` surfaces.
- Reference: `UI/CLAUDE.md`, `.github/instructions/ui.instructions.md`.
