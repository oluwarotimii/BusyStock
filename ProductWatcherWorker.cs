using WatcherService.Models;
using WatcherService.Services;

namespace WatcherService
{
    public class ProductWatcherWorker : BackgroundService
    {
        private readonly ILogger<ProductWatcherWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _pollingIntervalSeconds;
        private readonly int _batchSize;
        private readonly bool _useChangeTracking;
        private readonly TimeSpan _businessHoursStart;
        private readonly TimeSpan _businessHoursEnd;

        public ProductWatcherWorker(ILogger<ProductWatcherWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _pollingIntervalSeconds = int.Parse(configuration["PollingInterval:Seconds"] ?? "30"); // Default to 30 seconds
            _batchSize = int.Parse(configuration["BatchSettings:Size"] ?? "200"); // Default batch size
            _useChangeTracking = bool.Parse(configuration["SyncSettings:UseChangeTracking"] ?? "true");

            // Business hours configuration (default to 8 AM to 6 PM)
            var businessStartHour = int.Parse(configuration["BusinessHours:StartHour"] ?? "8");
            var businessEndHour = int.Parse(configuration["BusinessHours:EndHour"] ?? "18");
            _businessHoursStart = TimeSpan.FromHours(businessStartHour);
            _businessHoursEnd = TimeSpan.FromHours(businessEndHour);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Product Watcher Worker running at: {Time}", DateTimeOffset.Now);

            // Initialize change tracking if enabled
            if (_useChangeTracking)
            {
                using var scope = _serviceProvider.CreateScope();
                var changeTrackingService = scope.ServiceProvider.GetRequiredService<IChangeTrackingService>();
                await changeTrackingService.CreateTrackingTableIfNotExistsAsync();
            }

            // Use a timer to control the polling interval
            var initialInterval = TimeSpan.FromSeconds(_pollingIntervalSeconds);
            using var timer = new PeriodicTimer(initialInterval);

            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Check if we're in business hours if time-restricted mode is enabled
                var currentTime = DateTime.Now.TimeOfDay;
                var timeRestricted = bool.Parse(_serviceProvider.GetService<IConfiguration>()?["BusinessHours:RestrictSync"] ?? "false");

                if (!timeRestricted || (currentTime >= _businessHoursStart && currentTime <= _businessHoursEnd))
                {
                    await ProcessDataUpdate(stoppingToken);

                    // Adjust polling interval based on system load if adaptive mode is enabled
                    var useAdaptiveInterval = bool.Parse(_serviceProvider.GetService<IConfiguration>()?["LoadSettings:UseAdaptiveInterval"] ?? "true");
                    if (useAdaptiveInterval)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var systemMonitor = scope.ServiceProvider.GetRequiredService<ISystemMonitorService>();

                        try
                        {
                            var recommendedDelay = await systemMonitor.GetRecommendedDelayAsync();
                            _logger.LogDebug("Adjusting polling interval to {Delay}s based on system load", recommendedDelay.TotalSeconds);

                            // Update the timer with the new interval
                            timer.Period = recommendedDelay;

                            // Log database server metrics periodically
                            if (Random.Shared.NextDouble() > 0.7) // Log ~30% of the time to avoid excessive logging
                            {
                                var dbMetrics = await systemMonitor.GetDatabaseServerMetricsAsync();
                                _logger.LogDebug("Database server metrics: {@DbMetrics}", dbMetrics);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not adjust polling interval based on system load, using default");
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Outside business hours, skipping sync");
                }
            }

            _logger.LogInformation("Product Watcher Worker stopped at: {Time}", DateTimeOffset.Now);
        }

        private async Task ProcessDataUpdate(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogDebug("Starting product data sync at {StartTime}", startTime);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductDataService>();
                var apiService = scope.ServiceProvider.GetRequiredService<IApiService>();
                var changeTrackingService = scope.ServiceProvider.GetRequiredService<IChangeTrackingService>();
                var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitorService>();

                IEnumerable<ProductData> productData;
                var dataRetrievalStartTime = DateTime.UtcNow;

                if (_useChangeTracking)
                {
                    // Get incremental changes using change tracking
                    productData = await GetIncrementalProductData(scope);
                }
                else
                {
                    // Fallback to full data sync
                    var lastSyncTime = await changeTrackingService.GetLastSyncTimeAsync();
                    productData = await productService.GetProductDataIncrementalAsync(lastSyncTime);
                }

                var dataRetrievalDuration = DateTime.UtcNow - dataRetrievalStartTime;
                _logger.LogDebug("Data retrieval completed in {DurationMs}ms for {Count} products",
                    dataRetrievalDuration.TotalMilliseconds, productData.Count());

                // Record data retrieval performance
                performanceMonitor.RecordMetric("DataRetrieval.DurationMs", dataRetrievalDuration.TotalMilliseconds, DateTime.UtcNow);
                performanceMonitor.RecordMetric("DataRetrieval.RecordCount", productData.Count(), DateTime.UtcNow);

                if (productData.Any())
                {
                    // Only log when sending data
                    _logger.LogInformation("Sending {Count} records", productData.Count());

                    // Send data in batches to avoid overwhelming the API
                    var batches = productData
                        .Select((product, index) => new { product, index })
                        .GroupBy(x => x.index / _batchSize)
                        .Select(g => g.Select(x => x.product));

                    var totalBatches = batches.Count();
                    var successfulBatches = 0;
                    var failedBatches = 0;

                    var transmissionStartTime = DateTime.UtcNow;
                    foreach (var batch in batches)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var success = await apiService.SendProductDataAsync(batch);
                        if (!success)
                        {
                            _logger.LogWarning("Failed to send batch of {Count} products to API", batch.Count());
                            failedBatches++;
                        }
                        else
                        {
                            _logger.LogDebug("Successfully sent batch of {Count} products", batch.Count());
                            successfulBatches++;
                        }

                        // Small delay between batches to reduce server load
                        await Task.Delay(100, cancellationToken);
                    }
                    var transmissionDuration = DateTime.UtcNow - transmissionStartTime;

                    _logger.LogInformation("Sync completed: {Successful}/{Total} batches successful, " +
                        "retrieval took {RetrievalTime}ms, transmission took {TransmissionTime}ms",
                        successfulBatches, totalBatches,
                        (long)dataRetrievalDuration.TotalMilliseconds,
                        (long)transmissionDuration.TotalMilliseconds);

                    // Record transmission performance
                    performanceMonitor.RecordMetric("Transmission.DurationMs", transmissionDuration.TotalMilliseconds, DateTime.UtcNow);
                    performanceMonitor.RecordMetric("Transmission.BatchCount", totalBatches, DateTime.UtcNow);
                    performanceMonitor.RecordMetric("Transmission.SuccessfulBatches", successfulBatches, DateTime.UtcNow);
                    performanceMonitor.RecordMetric("Transmission.FailedBatches", failedBatches, DateTime.UtcNow);

                    // Update last sync time after successful sync
                    await changeTrackingService.SetLastSyncTimeAsync(DateTime.UtcNow);
                    await changeTrackingService.SetProductCountAtLastSyncAsync(productData.Count());
                }
                else
                {
                    _logger.LogDebug("No product changes detected since last sync");
                    performanceMonitor.RecordMetric("DataRetrieval.RecordCount", 0, DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Update failed after {DurationMs}ms: {Message}", duration.TotalMilliseconds, ex.Message);

                // Record the failure
                using var scope = _serviceProvider.CreateScope();
                var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitorService>();
                performanceMonitor.RecordMetric("Sync.Failure", 1, DateTime.UtcNow);
                performanceMonitor.RecordMetric("Sync.DurationMs", duration.TotalMilliseconds, DateTime.UtcNow);

                // Send alert for sync failure
                var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
                await alertService.SendAlertAsync("SYNC_FAILURE",
                    $"Product data sync failed: {ex.Message}",
                    new Dictionary<string, object>
                    {
                        ["DurationMs"] = duration.TotalMilliseconds,
                        ["ErrorMessage"] = ex.Message
                    });

                throw;
            }

            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogDebug("Product data sync completed in {DurationMs}ms", totalDuration.TotalMilliseconds);

            // Record total sync time
            using var scope = _serviceProvider.CreateScope();
            var performanceMonitor = scope.ServiceProvider.GetRequiredService<IPerformanceMonitorService>();
            performanceMonitor.RecordMetric("Sync.DurationMs", totalDuration.TotalMilliseconds, DateTime.UtcNow);

            // Check for performance degradation and trigger alerts if needed
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();

            // Get recent metrics to check for performance issues
            var recentMetrics = performanceMonitor.GetRecentMetrics(10); // Last 10 records
            await alertService.ProcessMetricsForAlertsAsync(recentMetrics);
        }

        private async Task<IEnumerable<ProductData>> GetIncrementalProductData(IServiceScope scope)
        {
            var productService = scope.ServiceProvider.GetRequiredService<IProductDataService>();
            var changeTrackingService = scope.ServiceProvider.GetRequiredService<IChangeTrackingService>();

            // Get changes from our tracking table
            var changes = await changeTrackingService.GetUnprocessedChangesAsync();

            if (!changes.Any())
            {
                // If no tracked changes, do a full comparison to detect changes
                return await productService.GetProductDataAsync();
            }

            // Get the specific products that have changed
            var changedProductCodes = changes.Select(c => c.ProductCode).Distinct();
            var changedProducts = await productService.GetProductsByCodesAsync(changedProductCodes);

            // Mark these changes as processed
            var changeIds = changes.Select(c => c.Id).ToList();
            await changeTrackingService.MarkChangesAsProcessedAsync(changeIds);

            return changedProducts;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Product Watcher Worker is stopping.");
            await base.StopAsync(cancellationToken);
        }
    }
}