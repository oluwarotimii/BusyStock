using System.Collections.Concurrent;

namespace WatcherService.Services
{
    public interface IPerformanceMonitorService
    {
        void RecordMetric(string metricName, double value, DateTime timestamp);
        Dictionary<string, double> GetAverageMetrics(TimeSpan timeWindow);
        Dictionary<string, double> GetRecentMetrics(int lastNRecords);
        void ResetMetrics();
        Task StartMonitoringAsync();
    }

    public class PerformanceMonitorService : IPerformanceMonitorService, IDisposable
    {
        private readonly ILogger<PerformanceMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly object _lock = new object();
        private readonly List<(string metricName, double value, DateTime timestamp)> _metrics;
        private readonly int _maxMetricsToStore;
        private readonly TimeSpan _metricRetentionPeriod;
        private readonly Timer _cleanupTimer;

        public PerformanceMonitorService(ILogger<PerformanceMonitorService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _metrics = new List<(string, double, DateTime)>();
            _maxMetricsToStore = int.Parse(configuration["PerformanceMonitor:MaxMetricsToStore"] ?? "10000");
            _metricRetentionPeriod = TimeSpan.FromHours(int.Parse(configuration["PerformanceMonitor:RetentionHours"] ?? "24"));

            // Set up periodic cleanup of old metrics
            _cleanupTimer = new Timer(CleanupOldMetrics, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        public void RecordMetric(string metricName, double value, DateTime timestamp)
        {
            lock (_lock)
            {
                _metrics.Add((metricName, value, timestamp));

                // If we have too many metrics, remove the oldest ones
                while (_metrics.Count > _maxMetricsToStore)
                {
                    _metrics.RemoveAt(0); // Remove the oldest (first) item
                }
            }

            _logger.LogTrace("Recorded metric: {MetricName} = {Value} at {Timestamp}", metricName, value, timestamp);
        }

        public Dictionary<string, double> GetAverageMetrics(TimeSpan timeWindow)
        {
            var cutoffTime = DateTime.UtcNow - timeWindow;

            lock (_lock)
            {
                var metricsInWindow = _metrics.Where(m => m.timestamp >= cutoffTime).ToList();

                var averages = new Dictionary<string, double>();
                var groupedMetrics = metricsInWindow.GroupBy(m => m.metricName);

                foreach (var group in groupedMetrics)
                {
                    averages[group.Key] = group.Average(m => m.value);
                }

                return averages;
            }
        }

        public Dictionary<string, double> GetRecentMetrics(int lastNRecords)
        {
            lock (_lock)
            {
                var recentMetrics = _metrics.TakeLast(lastNRecords).ToList();
                var averages = new Dictionary<string, double>();
                var groupedMetrics = recentMetrics.GroupBy(m => m.metricName);

                foreach (var group in groupedMetrics)
                {
                    averages[group.Key] = group.Average(m => m.value);
                }

                return averages;
            }
        }

        public async Task StartMonitoringAsync()
        {
            _logger.LogInformation("Performance monitoring started");
        }

        public void ResetMetrics()
        {
            lock (_lock)
            {
                _metrics.Clear();
            }
            _logger.LogInformation("Performance metrics reset");
        }

        private void CleanupOldMetrics(object state)
        {
            lock (_lock)
            {
                var cutoffTime = DateTime.UtcNow - _metricRetentionPeriod;
                _metrics.RemoveAll(m => m.timestamp < cutoffTime);
            }

            _logger.LogDebug("Cleaned up old metrics");
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}