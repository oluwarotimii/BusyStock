using System.ComponentModel.DataAnnotations;

namespace WatcherService.Models
{
    public class ProductChangeLog
    {
        [Key]
        public int Id { get; set; }
        
        public int ProductCode { get; set; }
        
        public string Operation { get; set; } = string.Empty; // "INSERT", "UPDATE", "DELETE"
        
        public DateTime ChangeTimestamp { get; set; }
        
        public bool Processed { get; set; } = false;
    }
    
    public class ProductSyncState
    {
        [Key]
        public int Id { get; set; } // Singleton pattern, Id = 1
        
        public DateTime LastSyncTime { get; set; }
        
        public int ProductCountAtLastSync { get; set; }
    }
}