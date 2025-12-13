using WatcherService.Models;
using System.Text;
using Newtonsoft.Json;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace WatcherService.Services
{
    public interface IApiService
    {
        Task<bool> SendProductDataAsync(IEnumerable<ProductData> productData);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiService> _logger;
        private readonly string _apiEndpoint;
        private readonly bool _useCompression;

        public ApiService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _apiEndpoint = configuration["ApiSettings:Endpoint"]
                ?? throw new ArgumentNullException("ApiSettings:Endpoint");
            _useCompression = bool.Parse(configuration["TransferSettings:UseCompression"] ?? "true");
        }

        public async Task<bool> SendProductDataAsync(IEnumerable<ProductData> productData)
        {
            var maxRetries = int.Parse(_configuration["RetrySettings:MaxRetries"] ?? "3");
            var baseDelay = TimeSpan.FromSeconds(double.Parse(_configuration["RetrySettings:BaseDelaySeconds"] ?? "1"));

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string json;
                    HttpContent content;

                    if (_useCompression && productData.Count() > 50) // Only compress if we have a significant amount of data
                    {
                        json = JsonConvert.SerializeObject(productData);
                        var compressedContent = CompressString(json);
                        content = new StreamContent(new MemoryStream(compressedContent));
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                        content.Headers.ContentEncoding.Add("gzip");

                        _logger.LogDebug("Compressed {OriginalLength} bytes to {CompressedLength} bytes ({Percent}% reduction)",
                            json.Length, compressedContent.Length, Math.Round((1.0 - (double)compressedContent.Length / json.Length) * 100, 2));
                    }
                    else
                    {
                        json = JsonConvert.SerializeObject(productData);
                        content = new StringContent(json, Encoding.UTF8, "application/json");
                    }

                    _logger.LogDebug("Attempting to send {Count} records to {Endpoint} (Attempt {Attempt})",
                        productData.Count(), _apiEndpoint, attempt + 1);

                    using var request = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint)
                    {
                        Content = content
                    };

                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        if (attempt > 0)
                        {
                            _logger.LogInformation("Successfully sent {Count} records on attempt {Attempt}",
                                productData.Count(), attempt + 1);
                        }
                        else
                        {
                            _logger.LogInformation("Successfully sent {Count} records", productData.Count());
                        }
                        return true;
                    }
                    else
                    {
                        // Check if this is a server error that warrants a retry
                        var shouldRetry = (int)response.StatusCode >= 500 || // Server errors
                                          response.StatusCode == System.Net.HttpStatusCode.RequestTimeout ||
                                          response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;

                        if (shouldRetry && attempt < maxRetries)
                        {
                            var delay = baseDelay * Math.Pow(2, attempt); // Exponential backoff
                            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)); // Add jitter to prevent thundering herd
                            var totalDelay = delay + jitter;

                            _logger.LogWarning("API request failed with {Status}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                                response.StatusCode, totalDelay.TotalSeconds, attempt + 1, maxRetries + 1);

                            await Task.Delay(totalDelay);
                            continue; // Try again
                        }
                        else
                        {
                            // Get more details about the error
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("API error after {Attempts} attempts: {Status}, Content: {Content}, Request Data Length: {DataLength}",
                                attempt + 1, response.StatusCode, errorContent, json.Length);

                            // Log a sample of the data being sent for debugging (first few records)
                            var sampleData = productData.Take(3).ToList();
                            _logger.LogDebug("Sample of data sent: {@SampleData}", sampleData);

                            return false;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("API request was cancelled");
                    throw; // Don't retry on cancellation
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        var delay = baseDelay * Math.Pow(2, attempt); // Exponential backoff
                        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)); // Add jitter
                        var totalDelay = delay + jitter;

                        _logger.LogWarning(ex, "API request failed due to exception, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                            totalDelay.TotalSeconds, attempt + 1, maxRetries + 1);

                        await Task.Delay(totalDelay);
                    }
                    else
                    {
                        _logger.LogError(ex, "API failed after {Attempts} attempts: {Message}", maxRetries + 1, ex.Message);
                        return false;
                    }
                }
            }

            _logger.LogError("Failed to send product data after {MaxRetries} retries", maxRetries);
            return false;
        }

        private byte[] CompressString(string text)
        {
            var buffer = Encoding.UTF8.GetBytes(text);
            using var memoryStream = new MemoryStream();
            using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gzipStream.Write(buffer, 0, buffer.Length);
            }
            return memoryStream.ToArray();
        }
    }
}