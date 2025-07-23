using BackgroundBotRunner;
using Communication;
using StateManager.Clients;
using StateManager.Listeners;
using StateManager.Repository;
using StateManager.Settings;
using System.Diagnostics;
using System.Text;
using static WinProcessImports;

namespace StateManager
{
    public class StateManagerWorker : BackgroundService
    {
        private readonly ILogger<StateManagerWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        private readonly CharacterStateSocketListener _activityMemberSocketListener;
        private readonly StateManagerSocketListener _worldStateManagerSocketListener;

        private readonly MangosSOAPClient _mangosSOAPClient;

        private readonly Dictionary<string, (IHostedService Service, CancellationTokenSource TokenSource, Task asyncTask)> _managedServices = [];

        public StateManagerWorker(
            ILogger<StateManagerWorker> logger,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _serviceProvider = serviceProvider;
            _configuration = configuration;

            _mangosSOAPClient = new MangosSOAPClient(configuration["MangosSOAP:IpAddress"]);

            _activityMemberSocketListener = new CharacterStateSocketListener(
                StateManagerSettings.Instance.CharacterDefinitions,
                configuration["CharacterStateListener:IpAddress"],
                int.Parse(configuration["CharacterStateListener:Port"]),
                _loggerFactory.CreateLogger<CharacterStateSocketListener>()
            );

            _logger.LogInformation($"Started ActivityMemberListener| {configuration["CharacterStateListener:IpAddress"]}:{configuration["CharacterStateListener:Port"]}");

            _worldStateManagerSocketListener = new StateManagerSocketListener(
                configuration["StateManagerListener:IpAddress"],
                int.Parse(configuration["StateManagerListener:Port"]),
                _loggerFactory.CreateLogger<StateManagerSocketListener>()
            );

            _logger.LogInformation($"Started StateManagerListener| {configuration["StateManagerListener:IpAddress"]}:{configuration["StateManagerListener:Port"]}");

            _worldStateManagerSocketListener.DataMessageSubject.Subscribe(OnWorldStateUpdate);
        }

        public void StartBackgroundBotWorker(string accountName)
        {
            var scope = _serviceProvider.CreateScope();
            var tokenSource = new CancellationTokenSource();
            var service = ActivatorUtilities.CreateInstance<BackgroundBotWorker>(
                scope.ServiceProvider,
                _loggerFactory,
                _configuration
            );

            _managedServices.Add(accountName, (service, tokenSource, Task.Run(async () => await service.StartAsync(tokenSource.Token))));
            _logger.LogInformation($"Started ActivityManagerService for account {accountName}");
        }

        public void StopManagedService(string accountName)
        {
            if (_managedServices.TryGetValue(accountName, out var serviceTuple))
            {
                serviceTuple.TokenSource.Cancel();
                
                // If it's a background service, stop it properly
                if (serviceTuple.Service != null)
                {
                    Task.Factory.StartNew(async () => await serviceTuple.Service.StopAsync(CancellationToken.None));
                }
                
                _managedServices.Remove(accountName);
                _logger.LogInformation($"Stopped managed service for account {accountName}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"StateManagerServiceWorker is running.");

            stoppingToken.Register(() => _logger.LogInformation($"StateManagerServiceWorker is stopping."));


            while (!stoppingToken.IsCancellationRequested)
            {
                await ApplyDesiredWorkerState(stoppingToken);
                await Task.Delay(5000, stoppingToken); // Check every 5 seconds
            }


            foreach (var (Service, TokenSource, Task) in _managedServices.Values)
                if (Service != null)
                {
                    await Service.StopAsync(TokenSource.Token);
                }

            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }

        private void OnWorldStateUpdate(AsyncRequest dataMessage)
        {
            StateChangeRequest stateChange = dataMessage.StateChange;

            if (stateChange != null)
            {

            }

            StateChangeResponse stateChangeResponse = new();
            _worldStateManagerSocketListener.SendMessageToClient(dataMessage.Id, stateChangeResponse);
        }

        private async Task<bool> ApplyDesiredWorkerState(CancellationToken stoppingToken)
        {
            for (int i = 0; i < StateManagerSettings.Instance.CharacterDefinitions.Count; i++)
                if (!_managedServices.ContainsKey(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName))
                {
                    if (!ReamldRepository.CheckIfAccountExists(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName))
                    {
                        await _mangosSOAPClient.CreateAccountAsync(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName);

                        await Task.Delay(100);

                        await _mangosSOAPClient.SetGMLevelAsync(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName, 3);
                    }
                        //StartBackgroundBotWorker(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName);
                        StartForegroundBotRunner(StateManagerSettings.Instance.CharacterDefinitions[i].AccountName);
                        await Task.Delay(100, stoppingToken);
                    

                    await Task.Delay(500);
                }

            return true;
        }

        private void StartForegroundBotRunner(string accountName)
        {
            const string PATH_TO_GAME = @"C:\Users\wowadmin\Desktop\Elysium Project Game Client\WoW.exe";

            var startupInfo = new STARTUPINFO();

            // run BloogsQuest.exe in a new process
            CreateProcess(
                PATH_TO_GAME,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                ProcessCreationFlag.CREATE_DEFAULT_ERROR_MODE,
                IntPtr.Zero,
                null,
                ref startupInfo,
                out PROCESS_INFORMATION processInfo);

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // get a handle to the BloogsQuest process
            var processHandle = Process.GetProcessById((int)processInfo.dwProcessId).Handle;

            // resolve the file path to Loader.dll relative to our current working directory
            var loaderPath = Path.Combine(@"C:\Users\wowadmin\RiderProjects\BloogBot\Bot\Debug\net8.0\Loader.dll");

            // allocate enough memory to hold the full file path to Loader.dll within the BloogsQuest process
            var loaderPathPtr = VirtualAllocEx(
                processHandle,
                (IntPtr)0,
                loaderPath.Length,
                MemoryAllocationType.MEM_COMMIT,
                MemoryProtectionType.PAGE_EXECUTE_READWRITE);

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // write the file path to Loader.dll to the BloogsQuest process's memory
            var bytes = Encoding.Unicode.GetBytes(loaderPath);
            var bytesWritten = 0; // throw away
            WriteProcessMemory(processHandle, loaderPathPtr, bytes, bytes.Length, ref bytesWritten);

            // search current process's for the memory address of the LoadLibraryW function within the kernel32.dll module
            var loaderDllPointer = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // create a new thread with the execution starting at the LoadLibraryW function, 
            // with the path to our Loader.dll passed as a parameter
            CreateRemoteThread(processHandle, (IntPtr)null, (IntPtr)0, loaderDllPointer, loaderPathPtr, 0, (IntPtr)null);

            // this seems to help prevent timing issues
            Thread.Sleep(500);

            // free the memory that was allocated by VirtualAllocEx
            VirtualFreeEx(processHandle, loaderPathPtr, 0, MemoryFreeType.MEM_RELEASE);

            // Track the launched process to prevent infinite launching
            var process = Process.GetProcessById((int)processInfo.dwProcessId);
            var tokenSource = new CancellationTokenSource();
            
            // Create a task that monitors the process and removes it from tracking when it exits
            var monitoringTask = Task.Run(async () =>
            {
                try
                {
                    while (!process.HasExited && !tokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, tokenSource.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error monitoring foreground bot process for account {accountName}");
                }
                finally
                {
                    // Remove from managed services when process exits
                    _managedServices.Remove(accountName);
                    _logger.LogInformation($"Foreground bot process for account {accountName} has exited and been removed from tracking");
                }
            });

            // Add to managed services to prevent relaunching
            _managedServices.Add(accountName, (null, tokenSource, monitoringTask));
            _logger.LogInformation($"Started Foreground Bot Runner for account {accountName} (Process ID: {processInfo.dwProcessId})");
        }
    }
    
}
