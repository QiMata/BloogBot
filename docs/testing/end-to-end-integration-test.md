# End-to-End Integration Test Setup Guide

## Overview

The `StateUpdateIntegrationTest` is a comprehensive end-to-end integration test that validates the complete bot injection and state tracking pipeline. This test uses **NO SIMULATION** - all components must be real and functional.

## Test Architecture

The integration test validates this complete pipeline:

```
1. StateManager starts WoW.exe process
2. StateManager injects Loader.dll into WoW.exe
3. Loader.dll loads .NET runtime and executes ForegroundBotRunner.dll
4. ForegroundBotRunner creates character in-game
5. Bot movement is commanded via ObjectManager
6. Character position changes are tracked in Mangos database
7. Test asserts that real state changes occurred
```

## Prerequisites

### 1. Required Services

Before running the integration test, ensure these services are running:

#### Mangos Server
- **Required**: Mangos server with SOAP enabled on port 7878
- **Database**: MySQL with character and auth databases accessible
- **Configuration**: SOAP remote access enabled

#### PathfindingService
```bash
cd Services/PathfindingService
dotnet run
```

#### StateManager (Optional - auto-started by test)
```bash
cd Services/StateManager
dotnet run
```

### 2. Build Requirements

#### C++ Components
Run the setup script to build injection DLLs:
```powershell
.\Setup-InjectionDlls.ps1
```

This builds:
- `Loader.dll` - C++ injection loader
- `ForegroundBotRunner.dll` - .NET bot runtime
- `ForegroundBotRunner.runtimeconfig.json` - .NET runtime configuration

#### .NET Components
```bash
dotnet build
```

### 3. Configuration Files

#### appsettings.test.json
Create `Tests/BotRunner.Tests/appsettings.test.json`:

```json
{
  "ConnectionStrings": {
    "CharacterDatabase": "Server=localhost;Database=characters;Uid=mangos;Pwd=mangos;",
    "AuthDatabase": "Server=localhost;Database=realmd;Uid=mangos;Pwd=mangos;",
    "MangosDatabase": "Server=localhost;Database=mangos;Uid=mangos;Pwd=mangos;"
  },
  "MangosSOAP": {
    "IpAddress": "http://127.0.0.1:7878"
  },
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": "5000"
  },
  "CharacterStateListener": {
    "IpAddress": "127.0.0.1",
    "Port": "8081"
  },
  "StateManagerListener": {
    "IpAddress": "127.0.0.1",
    "Port": "8080"
  },
  "RealmEndpoint": {
    "IpAddress": "127.0.0.1"
  },
  "GameClient": {
    "ExecutablePath": "C:\\Games\\World of Warcraft\\WoW.exe"
  },
  "LoaderDllPath": "Exports\\Bot\\Release\\net8.0\\Loader.dll",
  "Injection": {
    "AllocateConsole": "true"
  }
}
```

#### StateManager Character Definitions
Ensure `Services/StateManager/character-definitions.json` exists:

```json
[
  {
    "AccountName": "TESTBOT01",
    "CharacterName": "TestCharacter",
    "RealmName": "TestRealm",
    "Class": "Warrior",
    "Race": "Human"
  }
]
```

## Running the Test

### Command Line
```bash
cd Tests/BotRunner.Tests
dotnet test --filter "MangosServerStateTrackingIntegrationTest" --logger "console;verbosity=detailed"
```

### Visual Studio
1. Open Test Explorer
2. Find `BotMovement_ShouldUpdatePositionInMangosDatabase_RealInfrastructure`
3. Right-click ? Run Test
4. Monitor output in Test Output window

## Test Phases

### Phase 1: Infrastructure Validation
- ? Mangos SOAP connectivity
- ? Database connectivity  
- ? DLL injection prerequisites
- ? PathfindingService availability
- ? CharacterStateListener availability

### Phase 2: Real Bot Injection
- ?? StateManager starts WoW.exe
- ?? Loader.dll injection into WoW.exe
- ?? .NET runtime initialization
- ?? ForegroundBotRunner.dll execution
- ?? Character creation in-game
- ?? Bot enters game world

### Phase 3: Movement Testing
- ?? Calculate target position
- ??? Generate pathfinding route
- ?? Command bot movement via ObjectManager
- ?? Monitor movement progress
- ?? Wait for completion

### Phase 4: State Validation
- ?? Stop StateManager
- ?? Allow database updates to flush
- ?? Query final character position
- ?? Calculate distance moved
- ? Assert movement occurred
- ? Assert reasonable accuracy

## Expected Test Output

### Successful Test Run
```
=== REAL END-TO-END MANGOS SERVER STATE TRACKING INTEGRATION TEST ===
Using ACTUAL bot injection and in-game character creation - NO SIMULATION

1. ARRANGE: Validating infrastructure for real bot injection
   - Mangos SOAP Status: Connected
   - Character Database: Connected
   - ? Loader.dll found at: C:\...\Loader.dll
   - ? ForegroundBotRunner.dll found at: C:\...\ForegroundBotRunner.dll
   - ? WoW.exe found at: C:\Games\World of Warcraft\WoW.exe

2. ACT: Starting StateManager for REAL bot injection and character creation
   - ? PathfindingService connection established
   - ? CharacterStateListener available at 127.0.0.1:8081
   - Starting StateManager with real DLL injection enabled...
   - CHARACTER CREATED BY INJECTED BOT! Count: 1
   - BOT ENTERED WORLD! Position: (-8949.95, -132.493, 83.5312)

3. ACT: Commanding REAL bot movement via ObjectManager
   - Target Position: (-8934.95, -132.493, 83.5312)
   - Pathfinding calculated route with 5 waypoints
   - COMMANDED REAL BOT to move toward target via ObjectManager
   - REAL BOT has moved significantly or reached target!

4. ASSERT: Verifying REAL bot state changes in Mangos database
   - Distance Moved by REAL bot: 12.43 units
   - Distance from Target: 3.21 units

5. RESULTS: END-TO-END integration test validation SUCCESSFUL
   ? Real DLL injection completed successfully
   ? Real bot created character in-game (not via database)
   ? Real bot movement commanded via ObjectManager
   ? Real character position state change tracked in Mangos database
   ? Position updated from (-8949.95, -132.493, 83.5312) to (-8937.52, -132.493, 83.5312)
   ? Distance moved: 12.43 units
   ? END-TO-END test demonstrates REAL bot state changes are properly tracked!

=== INTEGRATION TEST COMPLETE: REAL BOT STATE TRACKING VERIFIED ===
```

## Troubleshooting

### Common Issues

#### 1. DLL Injection Failure
**Symptoms**: "Bot injection failed - no character creation detected"

**Causes**:
- Loader.dll not found or corrupted
- ForegroundBotRunner.dll missing dependencies
- .NET 8 runtime not available
- WoW.exe process protection
- Insufficient permissions

**Solutions**:
```powershell
# Rebuild injection components
.\Setup-InjectionDlls.ps1

# Verify .NET 8 runtime
dotnet --info

# Run as Administrator if needed
```

#### 2. PathfindingService Connection Failed
**Symptoms**: "PathfindingService connection failed"

**Solution**:
```bash
cd Services/PathfindingService
dotnet run
```

#### 3. Mangos SOAP Not Available
**Symptoms**: "Mangos SOAP server must be running"

**Solutions**:
- Start Mangos server with SOAP enabled
- Verify port 7878 is accessible
- Check firewall settings
- Verify SOAP configuration in mangosd.conf

#### 4. Database Connection Issues
**Symptoms**: "Character database connection string not configured"

**Solutions**:
- Verify MySQL server is running
- Check connection strings in appsettings.test.json
- Ensure database credentials are correct
- Test database connectivity manually

#### 5. WoW.exe Path Issues
**Symptoms**: "WoW.exe not found"

**Solution**:
Update `GameClient:ExecutablePath` in appsettings.test.json:
```json
{
  "GameClient": {
    "ExecutablePath": "C:\\Path\\To\\Your\\WoW.exe"
  }
}
```

### Advanced Debugging

#### Enable Loader Console Output
Set in appsettings.test.json:
```json
{
  "Injection": {
    "AllocateConsole": "true"
  }
}
```

#### Environment Variables for Debugging
```powershell
$env:LOADER_PAUSE_ON_EXCEPTION = "1"
$env:LOADER_ALLOC_CONSOLE = "1"
$env:LOADER_LOG_PATH = "C:\\temp\\loader.log"
```

#### Check Injection Breadcrumbs
After injection, check for these files:
- `testentry_stdcall.txt` - Indicates successful managed code execution
- `testentry_cdecl.txt` - Alternative calling convention success
- `loader_full_*.txt` - Detailed injection logs

## Test Architecture Details

### Key Components

#### StateManagerWorker
- Manages WoW.exe process lifecycle
- Handles DLL injection via Windows API
- Monitors bot process health
- Provides injection diagnostics

#### IntegrationTestFixture
- Configures real service dependencies
- Manages test service lifecycle
- Ensures infrastructure availability
- Handles cleanup on disposal

#### ObjectManager Integration
- Connects to injected bot's game state
- Provides real-time position tracking
- Commands actual bot movement
- Reports world entry status

### Database State Tracking

The test validates that character state changes in the game world are properly persisted to the Mangos character database:

```sql
-- Test queries the characters table
SELECT position_x, position_y, position_z 
FROM characters 
WHERE name = 'TestCharacter'
```

This ensures the complete pipeline from in-game actions to database persistence is working correctly.

## Continuous Integration

For CI/CD environments, this test requires:

1. **Mangos Server**: Full server setup with databases
2. **Game Client**: Accessible WoW.exe installation
3. **Build Environment**: Visual Studio Build Tools or VS2022
4. **Permissions**: Administrator rights for DLL injection
5. **Display**: Virtual display for graphics initialization

Consider using containerized Mangos servers and virtual displays for CI environments.

## Performance Expectations

- **Total Test Duration**: 3-7 minutes
- **Injection Time**: 30-60 seconds
- **Character Creation**: 30-90 seconds  
- **Movement Phase**: 60-120 seconds
- **Database Validation**: 5-10 seconds

The test includes generous timeouts to account for varying system performance and initialization delays.