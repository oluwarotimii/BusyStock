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

                var response = await _httpClient.PostAsync(_apiEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    // Only minimal logging - only when sending data
                    _logger.LogInformation("Sent {Count} records", productData.Count());
                    return true;
                }
                else
                {
                    _logger.LogWarning("API error: {Status}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("API failed: {Message}", ex.Message);
                return false;
            }
        }
    }
}