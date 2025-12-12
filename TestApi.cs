using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Simple test to check the API endpoint
public class TestApi
{
    public static async Task Main(string[] args)
    {
        var client = new HttpClient();
        
        // Sample data that mimics what would be sent
        var sampleData = new[]
        {
            new { 
                Code = 1, 
                ItemName = "Test Product", 
                PrintName = "Test Print", 
                SalePrice = 10.99m, 
                CostPrice = 5.99m, 
                TotalAvailableStock = 100m,
                LastModified = DateTime.UtcNow
            }
        };

        try
        {
            var json = JsonConvert.SerializeObject(sampleData);
            Console.WriteLine($"JSON being sent: {json}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            Console.WriteLine("Sending request to API endpoint...");
            var response = await client.PostAsync("https://femapp.vercel.app/api/sync/busy", content);
            
            Console.WriteLine($"Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Response Content: {responseContent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            client.Dispose();
        }
    }
}

public class ProductData
{
    public int Code { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string PrintName { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TotalAvailableStock { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}