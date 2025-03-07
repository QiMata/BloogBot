using ActivityBackgroundMember;
using Communication;
using Microsoft.Extensions.Options;
using StateManager.Listeners;
using StateManager.Settings;

namespace StateManager
{
    public class StateManagerWorker : BackgroundService
    {
        private readonly ILogger<StateManagerWorker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        private readonly Dictionary<string, (IHostedService Service, CancellationTokenSource TokenSource, Task asyncTask)> _managedServices = [];

        private readonly ActivityMemberSocketListener _activityMemberSocketListener;
        private readonly StateManagerSocketListener _worldStateManagerSocketListener;

        public IEnumerable<ActivitySnapshot> CurrentActivityMemberList { get; private set; } = [];

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

            _activityMemberSocketListener = new ActivityMemberSocketListener(
                configuration["ActivityMemberListener:IpAddress"],
                int.Parse(configuration["ActivityMemberListener:Port"]),
                _loggerFactory.CreateLogger<ActivityMemberSocketListener>()
            );

            _logger.LogInformation($"Started ActivityMemberListener| {configuration["ActivityMemberListener:IpAddress"]}:{configuration["ActivityMemberListener:Port"]}");

            _worldStateManagerSocketListener = new StateManagerSocketListener(
                configuration["StateManagerListener:IpAddress"],
                int.Parse(configuration["StateManagerListener:Port"]),
                _loggerFactory.CreateLogger<StateManagerSocketListener>()
            );

            _logger.LogInformation($"Started StateManagerListener| {configuration["StateManagerListener:IpAddress"]}:{configuration["StateManagerListener:Port"]}");

            _activityMemberSocketListener.DataMessageSubject.Subscribe(OnActivityManagerUpdate);
            _worldStateManagerSocketListener.DataMessageSubject.Subscribe(OnWorldStateUpdate);
        }

        public void StartManagedService(string accountName)
        {
            var scope = _serviceProvider.CreateScope();
            var tokenSource = new CancellationTokenSource();
            var service = ActivatorUtilities.CreateInstance<ActivityBackgroundMemberWorker>(
                scope.ServiceProvider,
                _loggerFactory,
                _loggerFactory.CreateLogger<ActivityBackgroundMemberWorker>(),
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
                Task.Factory.StartNew(async () => await serviceTuple.Service.StopAsync(CancellationToken.None));
                _managedServices.Remove(accountName);
                _logger.LogInformation($"Stopped ActivityManagerService for account {accountName}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"StateManagerServiceWorker is running.");

            stoppingToken.Register(() =>
                _logger.LogInformation($"StateManagerServiceWorker is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                // Here you can add logic to start/stop services based on certain conditions.
                await Task.Delay(10000, stoppingToken);
            }

            foreach (var (Service, TokenSource, Task) in _managedServices.Values)
                await Service.StopAsync(stoppingToken);

            _logger.LogInformation($"StateManagerServiceWorker has stopped.");
        }

        private void OnActivityManagerUpdate(AsyncRequest dataMessage)
        {
            //ActivityMemberState activityMemberState = dataMessage.ActivityMemberState;
            //ActivityMember? activityMember = CurrentActivityMemberList.FirstOrDefault(x => x.AccountName != activityMemberState.Member.AccountName);

            //if (activityMember != null)
            //{

            //}

            //_activityMemberSocketListener.SendMessageToClient(dataMessage.Id, CurrentActivityMemberList.First(x => x.AccountName == activityMemberState.Member.AccountName));
        }

        private void OnWorldStateUpdate(AsyncRequest dataMessage)
        {
            //_worldStateManagerSocketListener.SendMessageToClient(dataMessage.Id, responseMessage);
        }

        private void ApplyDesiredState()
        {
            for (int i = 0; i < StateManagerSettings.Instance.ActivityMemberPresets.Count; i++)
                if (!CurrentActivityMemberList.Any(x => x.AccountName == StateManagerSettings.Instance.ActivityMemberPresets[i].AccountName))
                    StartManagedService(StateManagerSettings.Instance.ActivityMemberPresets[i].AccountName);
        }
    }
}
