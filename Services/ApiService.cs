using WatcherService.Models;
using System.Text;
using Newtonsoft.Json;

namespace WatcherService.Services
{
    public interface IApiService
    {
        Task<bool> SendProductDataAsync(IEnumerable<ProductData> productData);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;
        private readonly string _apiEndpoint;

        public ApiService(HttpClient httpClient, IConfiguration configuration, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiEndpoint = configuration["ApiSettings:Endpoint"]
                ?? throw new ArgumentNullException("ApiSettings:Endpoint");
        }

        public async Task<bool> SendProductDataAsync(IEnumerable<ProductData> productData)
        {
            try
            {
                var json = JsonConvert.SerializeObject(productData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Attempting to send {Count} records to {Endpoint}", productData.Count(), _apiEndpoint);

                var response = await _httpClient.PostAsync(_apiEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent {Count} records", productData.Count());
                    return true;
                }
                else
                {
                    // Get more details about the error
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("API error: {Status}, Content: {Content}, Request Data Length: {DataLength}",
                        response.StatusCode, errorContent, json.Length);

                    // Log a sample of the data being sent for debugging (first few records)
                    var sampleData = productData.Take(3).ToList();
                    _logger.LogDebug("Sample of data sent: {@SampleData}", sampleData);

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API failed: {Message}", ex.Message);
                return false;
            }
        }
    }
}