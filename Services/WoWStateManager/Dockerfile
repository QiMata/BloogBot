FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ["WestworldOfWarcraft.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Services/WoWStateManager/WoWStateManager.csproj", "Services/WoWStateManager/"]
COPY ["Services/BackgroundBotRunner/BackgroundBotRunner.csproj", "Services/BackgroundBotRunner/"]
COPY ["Services/DecisionEngineService/DecisionEngineService.csproj", "Services/DecisionEngineService/"]
COPY ["Services/ForegroundBotRunner/ForegroundBotRunner.csproj", "Services/ForegroundBotRunner/"]
COPY ["Services/PromptHandlingService/PromptHandlingService.csproj", "Services/PromptHandlingService/"]
COPY ["Exports/BotRunner/BotRunner.csproj", "Exports/BotRunner/"]
COPY ["Exports/WoWSharpClient/WoWSharpClient.csproj", "Exports/WoWSharpClient/"]
COPY ["Exports/BotCommLayer/BotCommLayer.csproj", "Exports/BotCommLayer/"]
COPY ["Exports/GameData.Core/GameData.Core.csproj", "Exports/GameData.Core/"]
COPY ["Exports/WinImports/WinProcessImports.csproj", "Exports/WinImports/"]
COPY ["BotProfiles/BotProfiles.csproj", "BotProfiles/"]
RUN dotnet restore "Services/WoWStateManager/WoWStateManager.csproj"
RUN dotnet restore "Services/BackgroundBotRunner/BackgroundBotRunner.csproj"

COPY . .

RUN dotnet publish "Services/WoWStateManager/WoWStateManager.csproj" -c Release -r win-x86 --self-contained true -p:Platform=x86 -o C:/out/wsm
RUN dotnet publish "Services/BackgroundBotRunner/BackgroundBotRunner.csproj" -c Release -r win-x64 --self-contained true -o C:/out/wsm/BackgroundBotRunner

FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR /app
COPY --from=build C:/out/wsm/ ./
ENTRYPOINT ["C:\\app\\WoWStateManager.exe"]
