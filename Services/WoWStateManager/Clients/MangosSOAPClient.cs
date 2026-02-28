namespace WoWStateManager.Clients
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Microsoft.Extensions.Logging;

    public class MangosSOAPClient
    {
        private readonly ILogger<MangosSOAPClient> _logger;
        public const string ServerInfoCommand = "server info";
        private readonly string _mangosBaseUrl;
        private readonly string _mangosHost;
        private readonly int _mangosPort;

        public MangosSOAPClient(string mangosUrl, ILogger<MangosSOAPClient> logger)
        {
            _logger = logger;
            // Parse the URL to extract host and construct full URL with port
            var uri = new Uri(mangosUrl.EndsWith(":7878") ? mangosUrl : $"{mangosUrl}:7878");
            _mangosHost = uri.Host;
            _mangosPort = uri.Port;
            _mangosBaseUrl = uri.ToString();
        }

        public async Task<bool> CheckSOAPPortStatus()
        {
            _logger.LogInformation($"Attempting SOAP connection to {_mangosHost}:{_mangosPort}");
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(_mangosHost, _mangosPort);
                var completedTask = await Task.WhenAny(task, Task.Delay(1000));
                bool connected = task.IsCompleted && client.Connected;
                _logger.LogInformation(connected ? "SOAP connection succeeded" : "SOAP connection failed");
                return connected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SOAP port status");
                return false;
            }
        }

        public async Task<string> CreateAccountAsync(string accountName) => await ExecuteGMCommandAsync($".account create {accountName} PASSWORD");
        public async Task<string> SetGMLevelAsync(string accountName, int gmLevel) => await ExecuteGMCommandAsync($".account set gmlevel {accountName} {gmLevel}");

        public async Task<string> ExecuteGMCommandAsync(string gmCommand)
        {
            _logger.LogInformation($"Issuing GM command: {gmCommand}");

            var xmlPayload = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
            <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
              <soap:Body>
                <ns1:executeCommand xmlns:ns1=""urn:MaNGOS"">
                  <command>{gmCommand}</command>
                </ns1:executeCommand>
              </soap:Body>
            </soap:Envelope>";

            using var client = new HttpClient();
            var byteArray = Encoding.ASCII.GetBytes($"ADMINISTRATOR:PASSWORD");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var content = new StringContent(xmlPayload, Encoding.UTF8, "text/xml");

            try
            {
                var response = await client.PostAsync(_mangosBaseUrl, content);

                // Get the response content even if status is not successful for debugging
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"SOAP request failed: {(int)response.StatusCode} ({response.StatusCode}). Response: {responseContent}");
                    return string.Empty;
                }

                _logger.LogDebug($"SOAP raw response: {responseContent}");

                var xml = XDocument.Parse(responseContent);

                // Check for SOAP fault
                var faultString = xml.Descendants("faultstring").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(faultString))
                {
                    _logger.LogWarning($"SOAP fault for '{gmCommand}': {faultString}");

                    // "There is no such command." = command doesn't exist in MaNGOS command table — always a bug.
                    if (faultString.Contains("no such command", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"GM command not found in MaNGOS command table: '{gmCommand}'. " +
                            $"SOAP fault: {faultString}. Fix the command or remove it.");
                    }

                    return $"FAULT: {faultString}";
                }

                // Try namespace-qualified result first, then unqualified
                var result = xml.Descendants(XName.Get("result", "urn:MaNGOS")).FirstOrDefault()?.Value
                          ?? xml.Descendants("result").FirstOrDefault()?.Value;

                _logger.LogInformation($"GM command response: {result}");

                return result ?? "OK (no output)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing GM command: {gmCommand}");
                return string.Empty;
            }
        }
    }
}
