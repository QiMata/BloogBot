using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class WowServerConfig
{
    public string DbUser { get; init; } = "app";
    public string DbPassword { get; init; } = "app";
    public string DbContainerImage { get; init; } = "ragedunicorn/mysql";
    public string DbContainerTag { get; init; } = "latest";
    public string ServerContainerImage { get; init; } = "ragedunicorn/wow-vanilla";
    public string ServerContainerTag { get; init; } = "latest";
    public PortSettings Ports { get; init; } = new();
    public VolumeSettings Volumes { get; init; } = new();
    public PathSettings Paths { get; init; } = new();

    public string DatabaseImage => BuildImage(DbContainerImage, DbContainerTag);

    public string ServerImage => BuildImage(ServerContainerImage, ServerContainerTag);

    public static WowServerConfig Load(IConfiguration configuration)
    {
        var section = configuration.GetSection("WowServer");
        var database = section.GetSection("Database");
        var server = section.GetSection("Server");
        var ports = section.GetSection("Ports");
        var volumes = section.GetSection("Volumes");
        var paths = section.GetSection("Paths");

        return new WowServerConfig
        {
            DbUser = Read(database, "User", "app"),
            DbPassword = Read(database, "Password", "app"),
            DbContainerImage = Read(database, "Image", "ragedunicorn/mysql"),
            DbContainerTag = Read(database, "Tag", "latest"),
            ServerContainerImage = Read(server, "Image", "ragedunicorn/wow-vanilla"),
            ServerContainerTag = Read(server, "Tag", "latest"),
            Ports = new PortSettings
            {
                MySql = ReadInt(ports, "MySql", 3306),
                MangosWorld = ReadInt(ports, "MangosWorld", 8085),
                MangosRealm = ReadInt(ports, "MangosRealm", 3724)
            },
            Volumes = new VolumeSettings
            {
                MySqlData = Read(volumes, "MySqlData", "wow_vanilla_mysql_data"),
                MySqlPath = Read(volumes, "MySqlPath", "/var/lib/mysql"),
                LogData = Read(volumes, "LogData", "wow_vanilla_log_data"),
                LogPath = Read(volumes, "LogPath", "/var/log/wow")
            },
            Paths = new PathSettings
            {
                BaseDirectory = paths["BaseDirectory"],
                ConfigDir = Read(paths, "ConfigDir", "config"),
                DataDir = Read(paths, "DataDir", "data"),
                ServerConfigPath = Read(paths, "ServerConfigPath", "/opt/vanilla/etc"),
                ServerDataPath = Read(paths, "ServerDataPath", "/opt/vanilla/data")
            }
        };
    }

    public ResolvedPathSettings ResolvePaths()
    {
        var baseDirectory = ResolveBaseDirectory(Paths.BaseDirectory);
        var configDir = ResolvePath(baseDirectory, Paths.ConfigDir);
        var dataDir = ResolvePath(baseDirectory, Paths.DataDir);

        return new ResolvedPathSettings(
            baseDirectory,
            configDir,
            dataDir,
            Path.Combine(configDir, "mangosd.conf.tpl"),
            Path.Combine(configDir, "realmd.conf.tpl"),
            Path.Combine(dataDir, "dbc"),
            Path.Combine(dataDir, "maps"),
            Path.Combine(dataDir, "mmaps"),
            Path.Combine(dataDir, "vmaps"));
    }

    public void ValidateBindMountSources(ResolvedPathSettings resolvedPaths)
    {
        var missing = new List<string>();

        RequireFile(resolvedPaths.MangosConfigTemplate, missing);
        RequireFile(resolvedPaths.RealmdConfigTemplate, missing);
        RequireDirectory(resolvedPaths.DbcDirectory, missing);
        RequireDirectory(resolvedPaths.MapsDirectory, missing);
        RequireDirectory(resolvedPaths.MmapsDirectory, missing);
        RequireDirectory(resolvedPaths.VmapsDirectory, missing);

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Systems.AppHost cannot start because required bind-mount sources are missing:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, missing.Select(path => $" - {path}")));
        }
    }

    private static string BuildImage(string image, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            return image;
        }

        return $"{image}:{tag}";
    }

    private static string ResolveBaseDirectory(string? configuredBaseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseDirectory))
        {
            return Path.GetFullPath(configuredBaseDirectory);
        }

        foreach (var candidate in EnumerateCandidateDirectories())
        {
            if (File.Exists(Path.Combine(candidate, "Systems.AppHost.csproj")))
            {
                return candidate;
            }

            var projectDirectory = Path.Combine(candidate, "UI", "Systems", "Systems.AppHost");
            if (File.Exists(Path.Combine(projectDirectory, "Systems.AppHost.csproj")))
            {
                return projectDirectory;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static IEnumerable<string> EnumerateCandidateDirectories()
    {
        foreach (var root in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                yield return directory.FullName;
                directory = directory.Parent;
            }
        }
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path));
    }

    private static string Read(IConfigurationSection section, string key, string fallback)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int ReadInt(IConfigurationSection section, string key, int fallback)
    {
        return int.TryParse(section[key], out var value) ? value : fallback;
    }

    private static void RequireFile(string path, ICollection<string> missing)
    {
        if (!File.Exists(path))
        {
            missing.Add(path);
        }
    }

    private static void RequireDirectory(string path, ICollection<string> missing)
    {
        if (!Directory.Exists(path))
        {
            missing.Add(path);
        }
    }
}

public sealed class PortSettings
{
    public int MySql { get; init; } = 3306;
    public int MangosWorld { get; init; } = 8085;
    public int MangosRealm { get; init; } = 3724;
}

public sealed class VolumeSettings
{
    public string MySqlData { get; init; } = "wow_vanilla_mysql_data";
    public string MySqlPath { get; init; } = "/var/lib/mysql";
    public string LogData { get; init; } = "wow_vanilla_log_data";
    public string LogPath { get; init; } = "/var/log/wow";
}

public sealed class PathSettings
{
    public string? BaseDirectory { get; init; }
    public string ConfigDir { get; init; } = "config";
    public string DataDir { get; init; } = "data";
    public string ServerConfigPath { get; init; } = "/opt/vanilla/etc";
    public string ServerDataPath { get; init; } = "/opt/vanilla/data";
}

public sealed record ResolvedPathSettings(
    string BaseDirectory,
    string ConfigDir,
    string DataDir,
    string MangosConfigTemplate,
    string RealmdConfigTemplate,
    string DbcDirectory,
    string MapsDirectory,
    string MmapsDirectory,
    string VmapsDirectory);
