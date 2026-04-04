using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LoadTests;

/// <summary>
/// Creates N MaNGOS accounts via the SOAP API. Idempotent — skips accounts that already exist.
/// Uses the SOAP endpoint at http://127.0.0.1:7878/ with ADMINISTRATOR:PASSWORD credentials.
/// </summary>
public static class BulkAccountCreator
{
    private const string SoapUrl = "http://127.0.0.1:7878/";
    private const string SoapUser = "ADMINISTRATOR";
    private const string SoapPassword = "PASSWORD";
    private const string DefaultBotPassword = "PASSWORD";

    /// <summary>
    /// Create accounts for the given bot configs. Sets GM level 6 on each.
    /// Returns (created, skipped, failed) counts.
    /// </summary>
    public static async Task<(int Created, int Skipped, int Failed)> CreateAccountsAsync(
        IReadOnlyList<BotDistribution.BotConfig> configs,
        int gmLevel = 6,
        Action<string>? log = null)
    {
        int created = 0, skipped = 0, failed = 0;

        using var client = new HttpClient();
        var authBytes = Encoding.ASCII.GetBytes($"{SoapUser}:{SoapPassword}");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        foreach (var config in configs)
        {
            var result = await ExecuteSoapCommandAsync(client,
                $".account create {config.AccountName} {DefaultBotPassword}");

            if (result.Contains("Account created", StringComparison.OrdinalIgnoreCase))
            {
                created++;
                log?.Invoke($"  Created: {config.AccountName} ({config.Race} {config.Class})");
            }
            else if (result.Contains("already exist", StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
            }
            else
            {
                failed++;
                log?.Invoke($"  FAILED: {config.AccountName} — {result}");
            }

            // Set GM level
            if (!result.Contains("FAULT", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteSoapCommandAsync(client,
                    $".account set gmlevel {config.AccountName} {gmLevel}");
            }
        }

        return (created, skipped, failed);
    }

    /// <summary>
    /// Generate N bot configs and create all accounts via SOAP.
    /// </summary>
    public static async Task<(int Created, int Skipped, int Failed)> CreateBulkAsync(
        int botCount, int gmLevel = 6, Action<string>? log = null)
    {
        var configs = BotDistribution.Generate(botCount);
        log?.Invoke($"Creating {configs.Count} accounts across {BotDistribution.AllCombos.Count} race/class combos...");
        return await CreateAccountsAsync(configs, gmLevel, log);
    }

    private static async Task<string> ExecuteSoapCommandAsync(HttpClient client, string command)
    {
        var soapBody = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<SOAP-ENV:Envelope xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns1=""urn:MaNGOS"">
<SOAP-ENV:Body><ns1:executeCommand><command>{System.Security.SecurityElement.Escape(command)}</command>
</ns1:executeCommand></SOAP-ENV:Body></SOAP-ENV:Envelope>";

        try
        {
            var response = await client.PostAsync(SoapUrl,
                new StringContent(soapBody, Encoding.UTF8, "text/xml"));
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"FAULT: {ex.Message}";
        }
    }
}
