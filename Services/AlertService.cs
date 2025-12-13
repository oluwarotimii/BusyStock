using WatcherService.Services;

namespace WatcherService.Services
{
    public interface IAlertService
    {
        Task SendAlertAsync(string alertType, string message, Dictionary<string, object>? context = null);
        bool IsAlertThresholdExceeded(string metricName, double value);
        Task ProcessMetricsForAlertsAsync(Dictionary<string, double> metrics);
    }

    public class AlertService : IAlertService
    {
        private readonly ILogger<AlertService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IPerformanceMonitorService _performanceMonitor;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _alertCooldownPeriod;
        private readonly Dictionary<string, DateTime> _lastAlertTime;

        public AlertService(ILogger<AlertService> logger, IConfiguration configuration,
            IPerformanceMonitorService performanceMonitor, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _performanceMonitor = performanceMonitor;
            _serviceProvider = serviceProvider;
            _alertCooldownPeriod = TimeSpan.FromMinutes(int.Parse(configuration["AlertSettings:CoolDownMinutes"] ?? "10"));
            _lastAlertTime = new Dictionary<string, DateTime>();
        }

        public async Task SendAlertAsync(string alertType, string message, Dictionary<string, object>? context = null)
        {
            var alertKey = $"{alertType}:{message}";
            
            // Check if we've sent this alert recently to avoid spam
            if (_lastAlertTime.ContainsKey(alertKey))
            {
                var timeSinceLastAlert = DateTime.UtcNow - _lastAlertTime[alertKey];
                if (timeSinceLastAlert < _alertCooldownPeriod)
                {
                    _logger.LogDebug("Alert suppressed due to cooldown: {AlertType}", alertType);
                    return;
                }
            }
            
            _logger.LogWarning("ALERT [{AlertType}]: {Message}", alertType, message);
            
            // Log context if provided
            if (context != null && context.Any())
            {
                foreach (var kvp in context)
                {
                    _logger.LogWarning("  {Key}: {Value}", kvp.Key, kvp.Value);
                }
            }
            
            // Store the time of this alert
            _lastAlertTime[alertKey] = DateTime.UtcNow;
            
            // TODO: In a real implementation, you might want to send the alert to an external system
            // like email, Slack, or a monitoring service
            
            // For now, we'll just log it with high importance
        }

        public bool IsAlertThresholdExceeded(string metricName, double value)
        {
            var threshold = GetAlertThreshold(metricName);
            return value > threshold;
        }

        private double GetAlertThreshold(string metricName)
        {
            return metricName switch
            {
                "Sync.DurationMs" => double.Parse(_configuration["AlertSettings:SyncDurationThresholdMs"] ?? "30000"), // 30 seconds
                "DataRetrieval.DurationMs" => double.Parse(_configuration["AlertSettings:DataRetrievalThresholdMs"] ?? "10000"), // 10 seconds
                "Transmission.DurationMs" => double.Parse(_configuration["AlertSettings:TransmissionThresholdMs"] ?? "15000"), // 15 seconds
                "Sync.Failure" => 0.5, // Any failure
                "FailedBatches" => double.Parse(_configuration["AlertSettings:FailedBatchesThreshold"] ?? "1"), // More than 1 failed batch
                "CpuUsagePercent" => double.Parse(_configuration["AlertSettings:CpuThresholdPercent"] ?? "90"),
                "MemoryUsageMB" => double.Parse(_configuration["AlertSettings:MemoryThresholdMB"] ?? "2000"),
                _ => double.MaxValue // Default to very high threshold for unknown metrics
            };
        }

        public async Task ProcessMetricsForAlertsAsync(Dictionary<string, double> metrics)
        {
            foreach (var metric in metrics)
            {
                if (IsAlertThresholdExceeded(metric.Key, metric.Value))
                {
                    var message = $"Metric '{metric.Key}' exceeded threshold: {metric.Value} (threshold: {GetAlertThreshold(metric.Key)})";
                    await SendAlertAsync("PERFORMANCE_DEGRADATION", message, new Dictionary<string, object> { ["Value"] = metric.Value });
                }
            }
        }
    }
}