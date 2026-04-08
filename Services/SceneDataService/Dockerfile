FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

RUN apt-get update || true \
    && apt-get install -y --no-install-recommends cmake build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY ["WestworldOfWarcraft.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Build.targets", "./"]
COPY ["Services/SceneDataService/SceneDataService.csproj", "Services/SceneDataService/"]
COPY ["Exports/BotCommLayer/BotCommLayer.csproj", "Exports/BotCommLayer/"]
COPY ["Exports/GameData.Core/GameData.Core.csproj", "Exports/GameData.Core/"]
RUN dotnet restore "Services/SceneDataService/SceneDataService.csproj"

COPY . .

RUN cmake -S Exports/Navigation -B /tmp/navigation-build -DCMAKE_BUILD_TYPE=Release \
    && cmake --build /tmp/navigation-build --config Release --parallel

RUN dotnet publish "Services/SceneDataService/SceneDataService.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o /app/publish

RUN cp /tmp/navigation-build/libNavigation.so /app/publish/libNavigation.so

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish/ ./

ENTRYPOINT ["dotnet", "SceneDataService.dll"]
