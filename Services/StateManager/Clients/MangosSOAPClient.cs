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

    public class MangosSOAPClient
    {
        public const string ServerInfoCommand = "server info";
        private readonly string _mangosBaseUrl;
        private readonly string _mangosHost;
        private readonly int _mangosPort;

        public MangosSOAPClient(string mangosUrl)
        {
            // Parse the URL to extract host and construct full URL with port
            var uri = new Uri(mangosUrl.EndsWith(":7878") ? mangosUrl : $"{mangosUrl}:7878");
            _mangosHost = uri.Host;
            _mangosPort = uri.Port;
            _mangosBaseUrl = uri.ToString();
        }

        public async Task<bool> CheckSOAPPortStatus()
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(_mangosHost, _mangosPort);
                var completedTask = await Task.WhenAny(task, Task.Delay(1000));
                return task.IsCompleted && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> CreateAccountAsync(string accountName) => await ExecuteGMCommandAsync($".account create {accountName} PASSWORD");
        public async Task<string> SetGMLevelAsync(string accountName, int gmLevel) => await ExecuteGMCommandAsync($".account set gmlevel {accountName} {gmLevel} -1");

        public async Task<string> ExecuteGMCommandAsync(string gmCommand)
        {
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
                    throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Response content: {responseContent}");
                }

                var xml = XDocument.Parse(responseContent);
                var result = xml.Descendants(XName.Get("result", "urn:MaNGOS")).FirstOrDefault()?.Value;

                return result ?? "No result element found.";
            }
            catch (HttpRequestException)
            {
                throw; // Re-throw with additional context already added
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing GM command '{gmCommand}': {ex.Message}", ex);
            }
        }
    }
}
