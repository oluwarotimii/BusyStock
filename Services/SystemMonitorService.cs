using System.Diagnostics;

namespace WatcherService.Services
{
    public interface ISystemMonitorService
    {
        Task<double> GetCpuUsageAsync();
        Task<double> GetMemoryUsageAsync();
        Task<double> GetDatabaseLoadEstimateAsync();
        Task<bool> IsSystemUnderHighLoadAsync();
        Task<TimeSpan> GetRecommendedDelayAsync();
        Task<Dictionary<string, object>> GetDatabaseServerMetricsAsync();
    }

    public class SystemMonitorService : ISystemMonitorService
    {
        private readonly ILogger<SystemMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<double> _cpuHistory = new();
        private readonly List<double> _memoryHistory = new();

        public SystemMonitorService(ILogger<SystemMonitorService> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public async Task<double> GetCpuUsageAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var startCpu = Process.GetCurrentProcess().TotalProcessorTime;

                await Task.Delay(500); // Sample over 500ms

                var endTime = DateTime.UtcNow;
                var endCpu = Process.GetCurrentProcess().TotalProcessorTime;

                var cpuUsedMs = (endCpu - startCpu).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;

                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
                
                // Add to history for trend analysis
                _cpuHistory.Add(cpuUsageTotal * 100);
                if (_cpuHistory.Count > 10) _cpuHistory.RemoveAt(0); // Keep last 10 readings

                return cpuUsageTotal * 100; // Return as percentage
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine CPU usage");
                return 0;
            }
        }

        public async Task<double> GetMemoryUsageAsync()
        {
            try
            {
                var process = Process.GetCurrentProcess();
                var memoryInBytes = process.WorkingSet64;
                var memoryInMB = memoryInBytes / (1024 * 1024);

                // Add to history for trend analysis
                _memoryHistory.Add(memoryInMB);
                if (_memoryHistory.Count > 10) _memoryHistory.RemoveAt(0); // Keep last 10 readings

                return memoryInMB;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine memory usage");
                return 0;
            }
        }

        public async Task<double> GetDatabaseLoadEstimateAsync()
        {
            try
            {
                // Get product count as a proxy for database size using the service provider
                using var scope = _serviceProvider.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductDataService>();

                var productCount = await productService.GetProductCountAsync();
                _logger.LogDebug("Estimated database load based on {Count} products", productCount);

                // Normalize to a 0-100 scale based on estimated thresholds
                if (productCount > 10000) return 90;  // Very high
                if (productCount > 5000) return 70;   // High
                if (productCount > 1000) return 40;   // Medium
                return 20; // Low
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not estimate database load");
                return 50; // Assume medium load on error
            }
        }

        public async Task<bool> IsSystemUnderHighLoadAsync()
        {
            var cpuThreshold = double.Parse(_configuration["LoadSettings:CpuThresholdPercent"] ?? "70");
            var memoryThreshold = double.Parse(_configuration["LoadSettings:MemoryThresholdMB"] ?? "1000");
            
            var cpuUsage = await GetCpuUsageAsync();
            var memoryUsage = await GetMemoryUsageAsync();

            var isHighLoad = cpuUsage > cpuThreshold || memoryUsage > memoryThreshold;
            _logger.LogDebug("System load check - CPU: {Cpu}%, Memory: {Memory}MB, High Load: {IsHighLoad}", 
                cpuUsage, memoryUsage, isHighLoad);

            return isHighLoad;
        }

        public async Task<TimeSpan> GetRecommendedDelayAsync()
        {
            var dbLoad = await GetDatabaseLoadEstimateAsync();
            var cpuUsage = await GetCpuUsageAsync();
            var memoryUsage = await GetMemoryUsageAsync();

            // Calculate base delay based on load factors
            var baseInterval = int.Parse(_configuration["PollingInterval:Seconds"] ?? "30");
            var delayMultiplier = 1.0;

            // Increase delay based on database size
            if (dbLoad > 80) delayMultiplier *= 3.0;  // Very large DB
            else if (dbLoad > 60) delayMultiplier *= 2.0; // Large DB
            else if (dbLoad > 40) delayMultiplier *= 1.5; // Medium DB

            // Increase delay based on CPU usage
            if (cpuUsage > 80) delayMultiplier *= 2.5;
            else if (cpuUsage > 60) delayMultiplier *= 1.5;
            else if (cpuUsage > 40) delayMultiplier *= 1.2;

            // Increase delay based on memory usage
            var memoryThreshold = double.Parse(_configuration["LoadSettings:MemoryThresholdMB"] ?? "1000");
            if (memoryUsage > memoryThreshold * 0.9) delayMultiplier *= 2.0;  // Near threshold
            else if (memoryUsage > memoryThreshold * 0.7) delayMultiplier *= 1.5; // Approaching threshold

            var recommendedDelay = TimeSpan.FromSeconds(baseInterval * delayMultiplier);
            
            // Apply min/max bounds
            var minDelay = TimeSpan.FromSeconds(int.Parse(_configuration["LoadSettings:MinDelaySeconds"] ?? "10"));
            var maxDelay = TimeSpan.FromSeconds(int.Parse(_configuration["LoadSettings:MaxDelaySeconds"] ?? "300"));
            
            if (recommendedDelay < minDelay) recommendedDelay = minDelay;
            if (recommendedDelay > maxDelay) recommendedDelay = maxDelay;

            return recommendedDelay;
        }

        public async Task<Dictionary<string, object>> GetDatabaseServerMetricsAsync()
        {
            var metrics = new Dictionary<string, object>();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var productService = scope.ServiceProvider.GetRequiredService<IProductDataService>();

                // Get database-specific metrics
                metrics["ProductCount"] = await productService.GetProductCountAsync();

                // Add query performance metrics by running a test query with timing
                var queryStartTime = DateTime.UtcNow;
                await productService.GetProductCountAsync(); // Run a simple query to measure performance
                var queryDuration = DateTime.UtcNow - queryStartTime;
                metrics["TestQueryDurationMs"] = queryDuration.TotalMilliseconds;

                // Flag if query is taking too long (indicating server load)
                metrics["QueryPerformanceThresholdExceeded"] = queryDuration.TotalMilliseconds >
                    double.Parse(_configuration["LoadSettings:MaxQueryTimeMs"] ?? "5000");

                // Add other relevant metrics as needed
                metrics["Timestamp"] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve database server metrics");
                metrics["Error"] = ex.Message;
            }

            return metrics;
        }
    }
}