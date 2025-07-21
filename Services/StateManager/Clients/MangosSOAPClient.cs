namespace StateManager.Clients
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

    public class MangosSOAPClient(string mangosUrl, ILogger<MangosSOAPClient> logger)
    {
        private readonly ILogger<MangosSOAPClient> _logger = logger;
        public const string ServerInfoCommand = "server info";

        public async Task<bool> CheckSOAPPortStatus()
        {
            _logger.LogInformation($"Attempting SOAP connection to {mangosUrl}:7878");
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(mangosUrl, 7878);
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
        public async Task<string> SetGMLevelAsync(string accountName, int gmLevel) => await ExecuteGMCommandAsync($".account set gmlevel {accountName} {gmLevel} -1");

        private async Task<string> ExecuteGMCommandAsync(string gmCommand)
        {
            try
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
                var response = await client.PostAsync(mangosUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var xml = XDocument.Parse(responseContent);
                var result = xml.Descendants(XName.Get("result", "urn:MaNGOS")).FirstOrDefault()?.Value;

                _logger.LogInformation($"GM command response: {result}");

                return result ?? "No result element found.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing GM command: {gmCommand}");
                return string.Empty;
            }
        }
    }
}
