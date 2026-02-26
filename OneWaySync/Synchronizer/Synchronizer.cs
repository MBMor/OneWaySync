using Microsoft.Extensions.Logging;

namespace OneWaySync.Synchronizer
{
    internal class Synchronizer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ISynchronizationProcessor _synchronizationProcessor;
        private readonly TimeSpan _synchronizationInterval;

        private Timer? _timer;
        private int _isSyncRunning_InterlockedUseOnly;

        public Synchronizer(
            ILogger logger,
            ISynchronizationProcessor synchronizationProcessor, 
            int synchronizationIntervalSeconds)
        {
            _logger = logger;
            _synchronizationProcessor = synchronizationProcessor;
            _synchronizationInterval = TimeSpan.FromSeconds(synchronizationIntervalSeconds);
        }

        public void Start()
        {
            if (_timer != null) 
                return;

            // Runs immediately SynchronizationCycle() - TimeSpan.Zero
            _timer = new Timer(_ => SynchronizationCycle(), null, TimeSpan.Zero, _synchronizationInterval);
            _logger.LogInformation("Synchronization started. Period: {Period}", _synchronizationInterval);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _logger.LogInformation("Synchronization stopped.");
        }
        private void SynchronizationCycle()
        {
            if (Interlocked.Exchange(ref _isSyncRunning_InterlockedUseOnly, 1) == 1)
            {
                _logger.LogWarning("Skipping start of synchronization cycle, previous one still in progress");
                return;
            }

            try
            {
                _synchronizationProcessor.RunOnce();

                _logger.LogInformation("Synchronization finished. Waiting for new round to start.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Sync failed: {Message}", ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _isSyncRunning_InterlockedUseOnly, 0);
            }
        }

        public void Dispose() => Stop();

    }
}
