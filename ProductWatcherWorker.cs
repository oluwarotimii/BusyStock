using WatcherService.Models;
using WatcherService.Services;

namespace WatcherService
{
    public class ProductWatcherWorker : BackgroundService
    {
        private readonly ILogger<ProductWatcherWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _pollingIntervalSeconds;
        private DateTime _lastCheckedTime;

        public ProductWatcherWorker(ILogger<ProductWatcherWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _pollingIntervalSeconds = int.Parse(configuration["PollingInterval:Seconds"] ?? "30"); // Default to 30 seconds
            _lastCheckedTime = DateTime.UtcNow.AddMinutes(-5); // Start by checking the last 5 minutes initially
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Product Watcher Worker running at: {Time}", DateTimeOffset.Now);

            // Use a timer to control the polling interval
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_pollingIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessDataUpdate(stoppingToken);

                // Update the last checked time after each successful run
                _lastCheckedTime = DateTime.UtcNow;
            }

            _logger.LogInformation("Product Watcher Worker stopped at: {Time}", DateTimeOffset.Now);
        }

        private async Task ProcessDataUpdate(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductDataService>();
                var apiService = scope.ServiceProvider.GetRequiredService<IApiService>();

                // Get product data
                var productData = await productService.GetProductDataIncrementalAsync(_lastCheckedTime);

                if (productData.Any())
                {
                    // Only log when sending data
                    _logger.LogInformation("Sending {Count} records", productData.Count());

                    var success = await apiService.SendProductDataAsync(productData);
                    if (!success)
                    {
                        _logger.LogWarning("Failed to send product data to API");
                    }
                }
            }
            catch (Exception ex)
            {
                // Only log critical errors
                _logger.LogError("Update failed: {Message}", ex.Message);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Product Watcher Worker is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}