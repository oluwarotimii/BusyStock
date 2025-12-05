using System.Text.Json.Serialization;

namespace WatcherService.Models
{
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
}