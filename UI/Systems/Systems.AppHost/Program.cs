using Aspire.Hosting;
using System;

var builder = DistributedApplication.CreateBuilder(args);

var wowConfig = WowServerConfig.Load(builder.Configuration);
var resolvedPaths = wowConfig.ResolvePaths();
wowConfig.ValidateBindMountSources(resolvedPaths);

Console.WriteLine("Systems.AppHost resolved bind-mount paths:");
Console.WriteLine($"  Base:   {resolvedPaths.BaseDirectory}");
Console.WriteLine($"  Config: {resolvedPaths.ConfigDir}");
Console.WriteLine($"  Data:   {resolvedPaths.DataDir}");
Console.WriteLine($"  DB image:  {wowConfig.DatabaseImage}");
Console.WriteLine($"  WoW image: {wowConfig.ServerImage}");
Console.WriteLine("Readiness: wow-vanilla-server waits for wow-vanilla-database before starting.");

var database = builder.AddContainer("wow-vanilla-database", wowConfig.DatabaseImage)
    .WithEnvironment("MYSQL_APP_USER", wowConfig.DbUser)
    .WithEnvironment("MYSQL_APP_PASSWORD", wowConfig.DbPassword)
    .WithVolume(wowConfig.Volumes.MySqlData, wowConfig.Volumes.MySqlPath)
    .WithEndpoint("mysql", endpoint =>
    {
        endpoint.TargetPort = wowConfig.Ports.MySql;
    });

var wowServer = builder.AddContainer("wow-vanilla-server", wowConfig.ServerImage)
    .WithEnvironment("MYSQL_APP_USER", wowConfig.DbUser)
    .WithEnvironment("MYSQL_APP_PASSWORD", wowConfig.DbPassword)
    .WithEnvironment("DATABASE_HOSTNAME", database.GetEndpoint("mysql"))
    .WithVolume(wowConfig.Volumes.LogData, wowConfig.Volumes.LogPath)
    .WithBindMount(resolvedPaths.MangosConfigTemplate, $"{wowConfig.Paths.ServerConfigPath}/mangosd.conf.tpl")
    .WithBindMount(resolvedPaths.RealmdConfigTemplate, $"{wowConfig.Paths.ServerConfigPath}/realmd.conf.tpl")
    .WithBindMount(resolvedPaths.DbcDirectory, $"{wowConfig.Paths.ServerDataPath}/dbc")
    .WithBindMount(resolvedPaths.MapsDirectory, $"{wowConfig.Paths.ServerDataPath}/maps")
    .WithBindMount(resolvedPaths.MmapsDirectory, $"{wowConfig.Paths.ServerDataPath}/mmaps")
    .WithBindMount(resolvedPaths.VmapsDirectory, $"{wowConfig.Paths.ServerDataPath}/vmaps")
    .WithEndpoint("mangos-world", endpoint =>
    {
        endpoint.TargetPort = wowConfig.Ports.MangosWorld;
    })
    .WithEndpoint("mangos-realm", endpoint =>
    {
        endpoint.TargetPort = wowConfig.Ports.MangosRealm;
    })
    .WithReference(database.GetEndpoint("mysql"));

wowServer.WaitFor(database);

var app = builder.Build();
app.Run();
