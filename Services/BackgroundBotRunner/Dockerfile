FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

COPY ["WestworldOfWarcraft.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Services/BackgroundBotRunner/BackgroundBotRunner.csproj", "Services/BackgroundBotRunner/"]
COPY ["Exports/BotRunner/BotRunner.csproj", "Exports/BotRunner/"]
COPY ["Exports/WoWSharpClient/WoWSharpClient.csproj", "Exports/WoWSharpClient/"]
COPY ["Exports/BotCommLayer/BotCommLayer.csproj", "Exports/BotCommLayer/"]
COPY ["Exports/GameData.Core/GameData.Core.csproj", "Exports/GameData.Core/"]
COPY ["Exports/WinImports/WinProcessImports.csproj", "Exports/WinImports/"]
COPY ["BotProfiles/BotProfiles.csproj", "BotProfiles/"]
RUN dotnet restore "Services/BackgroundBotRunner/BackgroundBotRunner.csproj"

COPY . .
RUN dotnet publish "Services/BackgroundBotRunner/BackgroundBotRunner.csproj" -c Release -r win-x64 --self-contained true -o C:/out/background

FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR /app
COPY --from=build C:/out/background/ ./
ENTRYPOINT ["C:\\app\\BackgroundBotRunner.exe"]
