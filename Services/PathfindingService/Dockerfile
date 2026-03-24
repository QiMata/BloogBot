FROM mcr.microsoft.com/dotnet/sdk:8.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

ADD https://aka.ms/vs/17/release/vc_redist.x64.exe C:/vc_redist.x64.exe
RUN C:\vc_redist.x64.exe /install /quiet /norestart || exit 1

COPY ["WestworldOfWarcraft.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Services/PathfindingService/PathfindingService.csproj", "Services/PathfindingService/"]
COPY ["Exports/BotCommLayer/BotCommLayer.csproj", "Exports/BotCommLayer/"]
COPY ["Exports/GameData.Core/GameData.Core.csproj", "Exports/GameData.Core/"]
COPY ["Exports/WinImports/WinProcessImports.csproj", "Exports/WinImports/"]
RUN dotnet restore "Services/PathfindingService/PathfindingService.csproj"

COPY . .
RUN dotnet publish "Services/PathfindingService/PathfindingService.csproj" -c Release -r win-x64 --self-contained true -o C:/out/pathfinding

FROM mcr.microsoft.com/windows/servercore:ltsc2022
WORKDIR /app

ADD https://aka.ms/vs/17/release/vc_redist.x64.exe C:/vc_redist.x64.exe
RUN C:\vc_redist.x64.exe /install /quiet /norestart || exit 1

COPY --from=build C:/out/pathfinding/ ./
ENTRYPOINT ["C:\\app\\PathfindingService.exe"]
