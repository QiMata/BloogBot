namespace WoWStateManager;

public class MangosServerOptions
{
    public string MangosDirectory { get; set; } = @"E:\repos\MaNGOS";
    public string MySqlRelativePath { get; set; } = @"mysql5\bin\mysqld.exe";
    public string MySqlArgs { get; set; } = "--console --max_allowed_packet=128M";
    public string RealmdExe { get; set; } = "realmd.exe";
    public string MangosdExe { get; set; } = "mangosd.exe";
    public int MySqlPort { get; set; } = 3306;
    public int RealmdPort { get; set; } = 3724;
    public int MangosdPort { get; set; } = 8085;
    public int SoapPort { get; set; } = 7878;
    public int MySqlTimeoutSeconds { get; set; } = 30;
    public int RealmdTimeoutSeconds { get; set; } = 15;
    public int MangosdTimeoutSeconds { get; set; } = 60;
    public bool AutoLaunch { get; set; } = true;
}
