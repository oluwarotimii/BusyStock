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
                    _logger.LogInformation("Successfully sent {Count} product records to API", productData.Count());
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to send data to API. Status: {Status}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending product data to API");
                return false;
            }
        }
    }
}