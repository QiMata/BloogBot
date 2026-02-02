# StateManagerUI

WPF desktop application for monitoring and controlling the WWoW bot system. Provides a visual interface for managing bot characters, viewing state, and configuring behavior.
A WPF desktop application that provides a graphical user interface for monitoring and managing World of Warcraft bot instances in the BloogBot ecosystem.

## Overview

StateManagerUI is a Windows desktop application that:
- **Displays Bot Status**: Real-time view of all connected bots
- **Controls Bot Behavior**: Start/stop/configure individual bots
- **Shows Game State**: Character health, position, targets, etc.
- **Integrates AI**: Chat interface for OpenAI-powered commands
StateManagerUI is a Windows Presentation Foundation (WPF) application built on .NET 8 that serves as the primary control center for managing multiple bot accounts, monitoring server status, and configuring character personalities through the Big Five personality model. It provides real-time monitoring of bot activities and allows operators to manage character states through an intuitive interface.

## Architecture
## Features

```
StateManagerUI/
??? App.xaml                      # Application entry
??? App.xaml.cs
??? MainWindow.xaml               # Main window layout
??? MainWindow.xaml.cs
??? Views/
?   ??? StateManagerViewModel.cs  # Main view model (MVVM)
??? Handlers/
?   ??? CommandHandler.cs         # ICommand implementation
??? Converters/
?   ??? ValueConverter.cs         # Generic value converter
?   ??? GreaterThanZeroToBooleanConverter.cs
??? BasicLogger.cs                # Simple logging
??? AssemblyInfo.cs
```
### Server Monitoring
- **Real-time Server Status**: Monitors MaNGOS realm and world server connectivity
- **Population Tracking**: Displays current server population statistics
- **Connection Health**: Visual indicators for server availability and response times
- **Automatic Polling**: Periodic status checks with configurable intervals

## Features
### Character Management
- **Multi-Character View**: Tabular display of all managed bot accounts
- **Activity Monitoring**: Real-time tracking of character actions and states
- **Character Selection**: Individual character focus for detailed management
- **Account Information**: Display of character names, races, classes, and levels

### Bot Status Display
### Personality Configuration
- **Big Five Model**: Adjustable personality traits for each character
  - **Openness**: Creativity and openness to new experiences
  - **Conscientiousness**: Organization and dependability
  - **Extraversion**: Social energy and assertiveness
  - **Agreeableness**: Cooperation and trust
  - **Neuroticism**: Emotional stability and stress response
- **Real-time Adjustment**: Live personality trait modification with immediate feedback
- **Value Precision**: Decimal precision for fine-tuned personality control

- Character name and level
- Current health/mana percentages
- Position (X, Y, Z coordinates)
- Current activity/state
- Connected/disconnected status
### State Management
- **Connection Management**: Connect and disconnect from StateManager service
- **Local State Loading**: Load character states from local configuration
- **Character Control**: Start and stop individual bot instances
- **Add/Remove Characters**: Dynamic character roster management

### Control Panel
## Architecture

- Start/Stop individual bots
- Pause/Resume execution
- Change target behavior
- Set grinding paths
### Core Components

### AI Chat Interface
- **MainWindow**: Primary WPF window hosting all UI elements
- **StateManagerViewModel**: MVVM pattern implementation with data binding
- **CommandHandler**: Command pattern implementation for user actions
- **Value Converters**: Data transformation for UI binding
- **BasicLogger**: Logging infrastructure for debugging and monitoring

Natural language bot control via Azure OpenAI:
### Technology Stack

```
User: "Go to Goldshire and grind boars until level 10"
AI: [Configures waypoints and grinding parameters]
```
- **.NET 8**: Modern cross-platform framework
- **WPF**: Windows Presentation Foundation for rich desktop UI
- **MVVM Pattern**: Model-View-ViewModel architectural pattern
- **Data Binding**: Two-way binding for real-time UI updates
- **Command Pattern**: Decoupled user interaction handling

## MVVM Pattern
## Dependencies

### ViewModel
### Project References
- **BotCommLayer**: Communication layer for inter-service messaging
- **GameData.Core**: Shared game data models and enumerations

```csharp
public class StateManagerViewModel : INotifyPropertyChanged
{
    public ObservableCollection<BotState> Bots { get; }
    public BotState SelectedBot { get; set; }
### NuGet Packages
- **Azure.AI.OpenAI**: Integration with Azure OpenAI services for enhanced AI capabilities

    public ICommand StartBotCommand { get; }
    public ICommand StopBotCommand { get; }
    public ICommand RefreshCommand { get; }
### Network Dependencies
- **StateManager Service**: Port 8088 for state management communication
- **Character State Service**: Port 5002 for character activity monitoring
- **MaNGOS Server**: Ports 3724 (realm) and 8085 (world) for server status

    // Property change notification
    public event PropertyChangedEventHandler PropertyChanged;
}
```
## Configuration

### Command Handler
### Network Configuration
The application connects to various services with default configurations:
- **StateManager**: `127.0.0.1:8088`
- **Character State Listener**: `127.0.0.1:5002`
- **Realm Server**: `127.0.0.1:3724`
- **World Server**: `127.0.0.1:8085`

```csharp
public class CommandHandler : ICommand
{
    private readonly Action<object> _execute;
    private readonly Func<object, bool> _canExecute;
### UI Configuration
- **Window Size**: 1024x384 pixels optimized for multi-monitor setups
- **Grid Layout**: 13x12 responsive grid for organized component placement
- **Font Size**: 10pt for optimal readability
- **Polling Interval**: 10-second server status updates

    public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object parameter) => _execute(parameter);
}
```
## User Interface

## Dependencies
### Main Dashboard
- **Server Status Panel**: Real-time realm and world server indicators
- **Connection Controls**: Connect, disconnect, and local state management
- **Character Grid**: Scrollable list of managed characters with selection
- **Personality Panel**: Big Five trait sliders with numeric input boxes

| Package | Version | Purpose |
|---------|---------|---------|
| Azure.AI.OpenAI | 1.0.0-beta.17 | AI chat integration |
### Character Information Display
- **Basic Info**: Name, race, class, level, continent, map
- **Current State**: Action and task status
- **Selection Controls**: Previous/next navigation with page indicators
- **Communication**: Text input for direct character commands

## Project References
### Control Buttons
- **Service Management**: Start and stop bot instances
- **Character Management**: Add and remove characters from roster
- **Navigation**: Character selection and pagination
- **Communication**: Send commands to selected character

- **BotCommLayer**: Protobuf communication with services
- **GameData.Core**: Game data models and interfaces
## Usage

## Configuration
### Starting the Application
1. Ensure StateManager service is running
2. Launch StateManagerUI.exe
3. Click "Connect" to establish service connection
4. Monitor server status indicators for connectivity

### Managing Characters
1. View character list in the main data grid
2. Select a character to view detailed information
3. Use Previous/Next buttons to navigate between characters
4. Add or remove characters using control buttons

Settings in `appsettings.json`:
### Configuring Personalities
1. Select a character from the grid
2. Adjust personality sliders for desired traits
3. Fine-tune values using numeric input boxes
4. Changes are applied in real-time to the character

```json
{
  "StateManager": {
    "Host": "localhost",
    "Port": 5001
  },
  "OpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4"
  }
}
```
### Monitoring Operations
1. Watch server status indicators for health
2. Monitor character activities in the grid
3. Check population counts for server load
4. Use logs for detailed troubleshooting

## Running
## Development

### Requirements
### Prerequisites
- **Visual Studio 2022** or later with WPF workload
- **.NET 8 SDK** or later
- **Windows 10/11** for WPF runtime support

- Windows 10/11 with .NET 8 runtime
- Windows SDK 10.0.22621.0
### Project Structure
```
StateManagerUI/
??? Views/
?   ??? StateManagerViewModel.cs    # Main view model
??? Converters/
?   ??? ValueConverter.cs           # Base converter class
?   ??? GreaterThanZeroToBooleanConverter.cs
??? Handlers/
?   ??? CommandHandler.cs           # Command pattern implementation
??? MainWindow.xaml                 # Main UI definition
??? MainWindow.xaml.cs             # Code-behind
??? App.xaml                        # Application definition
??? App.xaml.cs                     # Application logic
??? BasicLogger.cs                  # Logging utilities
```

### Launch
### Building
```bash
dotnet build StateManagerUI.csproj
```

### Running in Development
```bash
dotnet run --project UI/StateManagerUI
dotnet run --project StateManagerUI.csproj
```

Or run the compiled executable directly.
### Debugging
The application includes comprehensive logging through BasicLogger for troubleshooting connection issues, state management problems, and UI binding errors.

## Integration

### StateManager Service Integration
- **TCP Communication**: Direct socket communication for real-time updates
- **State Synchronization**: Bidirectional state management between UI and service
- **Event Handling**: Reactive updates based on character state changes

### Character State Updates
- **Real-time Monitoring**: Live activity tracking for all managed characters
- **State Persistence**: Character states maintained across application restarts
- **Multi-character Support**: Concurrent monitoring of multiple bot instances

## Troubleshooting

### Common Issues

**Connection Failed**
- Verify StateManager service is running on port 8088
- Check network configuration and firewall settings
- Ensure services are running on the same machine or network

**Character Not Displayed**
- Confirm character is registered with StateManager service
- Check character state listener connectivity on port 5002
- Verify character data synchronization

**Server Status Unknown**
- Validate MaNGOS server is running and accessible
- Check realm server on port 3724 and world server on port 8085
- Verify network connectivity to game servers

**Personality Changes Not Applied**
- Ensure character is selected before making changes
- Check StateManager service is connected and responsive
- Verify real-time communication is functioning

### Performance Considerations
- **Memory Usage**: Minimal impact with efficient WPF data binding
- **Network Overhead**: Low bandwidth usage with periodic polling
- **UI Responsiveness**: Asynchronous operations prevent UI blocking
- **Scalability**: Supports monitoring of 20+ concurrent characters

## Contributing

When contributing to StateManagerUI:

## Screenshots
1. Follow MVVM architectural patterns
2. Maintain WPF best practices for data binding
3. Ensure proper error handling and user feedback
4. Test with multiple character configurations
5. Validate UI responsiveness under load
6. Update documentation for new features

*(UI layout description)*
## License

```
???????????????????????????????????????????????????????????
? WWoW State Manager                              [?][?][×]?
???????????????????????????????????????????????????????????
? ??????????????????????? ??????????????????????????????? ?
? ? Bots                ? ? Selected Bot Details        ? ?
? ? ? BotChar1 (Lv 12)  ? ? Name: BotChar1              ? ?
? ? ? BotChar2 (Lv 8)   ? ? Level: 12                   ? ?
? ? ? BotChar3 (Lv 15)  ? ? Health: 850/1200 (70%)      ? ?
? ?                     ? ? Mana: 400/600 (66%)         ? ?
? ?                     ? ? Position: 1234, 5678, 90    ? ?
? ?                     ? ? State: Grinding             ? ?
? ??????????????????????? ? Target: Defias Bandit       ? ?
?                         ??????????????????????????????? ?
? ??????????????????????????????????????????????????????? ?
? ? [Start] [Stop] [Pause] [Configure]                  ? ?
? ??????????????????????????????????????????????????????? ?
? ??????????????????????????????????????????????????????? ?
? ? AI Chat                                             ? ?
? ? > Move to Westfall                                  ? ?
? ? Bot: Setting course for Westfall...                 ? ?
? ? > _                                                 ? ?
? ??????????????????????????????????????????????????????? ?
???????????????????????????????????????????????????????????
```
Part of the BloogBot project ecosystem.

## Related Documentation
## Related Projects

- See `Services/StateManager/README.md` for backend service
- See `ARCHITECTURE.md` for system overview
- **StateManager**: Core state management service
- **BotCommLayer**: Inter-service communication layer
- **GameData.Core**: Shared game data models
- **BackgroundBotRunner**: Individual bot instance management
- **PathfindingService**: Navigation and movement coordination