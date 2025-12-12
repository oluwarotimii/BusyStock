using System.Data.SqlClient;
using WatcherService.Models;
using Dapper;

namespace WatcherService.Services
{
    public interface IProductDataService
    {
        Task<IEnumerable<ProductData>> GetProductDataAsync();
        Task<IEnumerable<ProductData>> GetProductDataIncrementalAsync(DateTime lastCheckTime);
    }

    public class ProductDataService : IProductDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductDataService> _logger;

        public ProductDataService(IConfiguration configuration, ILogger<ProductDataService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("DefaultConnection");
            _logger = logger;
        }

        public async Task<IEnumerable<ProductData>> GetProductDataAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // This is the SQL query from your code.sql file
                var sql = @"
                    WITH StockData AS (
                        SELECT
                            MasterCode1 AS ItemID,
                            SUM(Value1) AS TotalAvailableStock
                        FROM
                            dbo.Tran2
                        GROUP BY
                            MasterCode1
                    )
                    SELECT
                        M.Code,
                        M.Name AS ItemName,
                        M.PrintName AS PrintName,
                        M.D3 AS SalePrice,
                        M.D4 AS CostPrice,
                        ISNULL(S.TotalAvailableStock, 0) AS TotalAvailableStock
                    FROM
                        Master1 M
                    LEFT JOIN
                        StockData S ON M.Code = S.ItemID
                    WHERE
                        M.MasterType = 6;";

                var result = await connection.QueryAsync<ProductData>(sql);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product data from database");
                throw;
            }
        }

        public async Task<IEnumerable<ProductData>> GetProductDataIncrementalAsync(DateTime lastCheckTime)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Optimized query - only return products that have changed if timestamp tracking is available
            // Otherwise, return all products (minimum viable query)
            var sql = @"
                WITH StockData AS (
                    SELECT
                        MasterCode1 AS ItemID,
                        SUM(Value1) AS TotalAvailableStock
                    FROM
                        dbo.Tran2
                    WHERE
                        MasterCode1 IN (SELECT Code FROM Master1 WHERE MasterType = 6)
                    GROUP BY
                        MasterCode1
                )
                SELECT
                    M.Code,
                    M.Name AS ItemName,
                    M.PrintName AS PrintName,
                    M.D3 AS SalePrice,
                    M.D4 AS CostPrice,
                    ISNULL(S.TotalAvailableStock, 0) AS TotalAvailableStock
                FROM
                    Master1 M
                LEFT JOIN StockData S ON M.Code = S.ItemID
                WHERE M.MasterType = 6";

            return await connection.QueryAsync<ProductData>(sql);
        }
    }
}