using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WatcherService.Models;

namespace WatcherService.Services
{
    public interface IChangeTrackingService
    {
        Task<bool> CreateTrackingTableIfNotExistsAsync();
        Task LogProductChangeAsync(int productCode, string operation);
        Task<List<ProductChangeLog>> GetUnprocessedChangesAsync();
        Task MarkChangesAsProcessedAsync(List<int> changeIds);
        Task<DateTime> GetLastSyncTimeAsync();
        Task SetLastSyncTimeAsync(DateTime syncTime);
        Task<int> GetProductCountAtLastSyncAsync();
        Task SetProductCountAtLastSyncAsync(int count);
    }

    public class ChangeTrackingService : IChangeTrackingService
    {
        private readonly string _connectionString;
        private readonly ILogger<ChangeTrackingService> _logger;

        public ChangeTrackingService(IConfiguration configuration, ILogger<ChangeTrackingService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("DefaultConnection");
            _logger = logger;
        }

        public async Task<bool> CreateTrackingTableIfNotExistsAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Create change tracking table if it doesn't exist
                var createTableSql = @"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductChangeLog' AND xtype='U')
                    BEGIN
                        CREATE TABLE ProductChangeLog (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            ProductCode INT NOT NULL,
                            Operation NVARCHAR(10) NOT NULL, -- INSERT, UPDATE, DELETE
                            ChangeTimestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
                            Processed BIT NOT NULL DEFAULT 0
                        );
                        
                        -- Create index for performance
                        CREATE INDEX IX_ProductChangeLog_ProductCode ON ProductChangeLog(ProductCode);
                        CREATE INDEX IX_ProductChangeLog_ChangeTimestamp ON ProductChangeLog(ChangeTimestamp);
                        CREATE INDEX IX_ProductChangeLog_Processed ON ProductChangeLog(Processed);
                    END

                    -- Create sync state table if it doesn't exist
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ProductSyncState' AND xtype='U')
                    BEGIN
                        CREATE TABLE ProductSyncState (
                            Id INT PRIMARY KEY DEFAULT 1, -- Singleton pattern
                            LastSyncTime DATETIME2 NOT NULL DEFAULT '1900-01-01',
                            ProductCountAtLastSync INT NOT NULL DEFAULT 0
                        );
                        
                        -- Insert initial record
                        INSERT INTO ProductSyncState (Id, LastSyncTime, ProductCountAtLastSync)
                        SELECT 1, '1900-01-01', 0
                        WHERE NOT EXISTS (SELECT * FROM ProductSyncState WHERE Id = 1);
                    END";

                await connection.ExecuteAsync(createTableSql);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tracking tables");
                return false;
            }
        }

        public async Task LogProductChangeAsync(int productCode, string operation)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    INSERT INTO ProductChangeLog (ProductCode, Operation, ChangeTimestamp, Processed)
                    VALUES (@ProductCode, @Operation, GETDATE(), 0)";

                await connection.ExecuteAsync(sql, new { ProductCode = productCode, Operation = operation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging product change for code {ProductCode}", productCode);
            }
        }

        public async Task<List<ProductChangeLog>> GetUnprocessedChangesAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT Id, ProductCode, Operation, ChangeTimestamp, Processed
                FROM ProductChangeLog
                WHERE Processed = 0
                ORDER BY ChangeTimestamp";

            var changes = await connection.QueryAsync<ProductChangeLog>(sql);
            return changes.ToList();
        }

        public async Task MarkChangesAsProcessedAsync(List<int> changeIds)
        {
            if (changeIds == null || !changeIds.Any()) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE ProductChangeLog
                SET Processed = 1
                WHERE Id IN @Ids";

            await connection.ExecuteAsync(sql, new { Ids = changeIds });
        }

        public async Task<DateTime> GetLastSyncTimeAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT LastSyncTime FROM ProductSyncState WHERE Id = 1";
            var result = await connection.QuerySingleOrDefaultAsync<DateTime?>(sql);
            return result ?? DateTime.MinValue;
        }

        public async Task SetLastSyncTimeAsync(DateTime syncTime)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE ProductSyncState SET LastSyncTime = @SyncTime WHERE Id = 1";
            await connection.ExecuteAsync(sql, new { SyncTime = syncTime });
        }

        public async Task<int> GetProductCountAtLastSyncAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "SELECT ProductCountAtLastSync FROM ProductSyncState WHERE Id = 1";
            var result = await connection.QuerySingleOrDefaultAsync<int?>(sql);
            return result ?? 0;
        }

        public async Task SetProductCountAtLastSyncAsync(int count)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var sql = "UPDATE ProductSyncState SET ProductCountAtLastSync = @Count WHERE Id = 1";
            await connection.ExecuteAsync(sql, new { Count = count });
        }
    }
}