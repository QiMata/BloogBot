# WoWStateManagerUI

WPF desktop application (.NET 8) for monitoring and managing bot instances. MVVM pattern with real-time character state display, server status polling, and Big Five personality configuration.

## Quick Start

```bash
# Build
dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release

# Run (requires StateManager on port 8088)
dotnet run --project UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release

# Test converters
dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release
```

## Ports

| Port | Service | Required |
|------|---------|----------|
| 8088 | StateManager API | Yes |
| 5002 | Character state IPC | Yes |
| 3724 | MaNGOS realm | Status panel |
| 8085 | MaNGOS world | Status panel |

## Project Structure

```
WoWStateManagerUI/
  App.xaml / MainWindow.xaml    # WPF entry point and layout (13x12 grid)
  Views/StateManagerViewModel.cs # MVVM view model, observable properties
  Handlers/CommandHandler.cs     # ICommand implementation
  Converters/                    # Value converters for XAML bindings
  BasicLogger.cs                 # ILogger implementation
```

## Converter Binding Contract

All converters are **one-way** (source to target only). `ConvertBack` either returns `Binding.DoNothing` or throws `NotSupportedException`.

| Converter | Input | Output | Binding usage |
|-----------|-------|--------|---------------|
| `GreaterThanZeroToBooleanConverter` | `int` | `bool` (`value > 0`) | `IsEnabled` on controls gated by `SelectCharacterIndex` (MainWindow.xaml:86-136) |
| `InverseBooleanConverter` | `bool` | `bool` (negated) | Inverts boolean properties for UI visibility/enabled state |
| `EnumDescriptionConverter` | `Enum` | `string` (from `[Description]` attribute) | Display-friendly enum labels |

`SelectCharacterIndex` defaults to `-1` (no selection). The `GreaterThanZeroToBooleanConverter` returns `false` for `-1` and `0`, disabling dependent controls until a valid character is selected (index >= 1).

Non-int values passed to `GreaterThanZeroToBooleanConverter` return `false` (safe fallback).

## Dependencies

- **BotCommLayer** (Protobuf IPC)
- **GameData.Core** (shared interfaces and enums)
- **Azure.AI.OpenAI** (NuGet, AI integration)
