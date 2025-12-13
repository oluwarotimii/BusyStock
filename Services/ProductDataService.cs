using System.Data.SqlClient;
using WatcherService.Models;
using Dapper;

namespace WatcherService.Services
{
    public interface IProductDataService
    {
        Task<IEnumerable<ProductData>> GetProductDataAsync();
        Task<IEnumerable<ProductData>> GetProductDataIncrementalAsync(DateTime lastCheckTime);
        Task<int> GetProductCountAsync();
        Task<IEnumerable<ProductData>> GetProductsByCodesAsync(IEnumerable<int> productCodes);
    }

    public class ProductDataService : IProductDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductDataService> _logger;
        private readonly int _queryTimeoutSeconds;

        public ProductDataService(IConfiguration configuration, ILogger<ProductDataService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection");
            _logger = logger;
            // Default to 30 seconds, but allow configuration override
            _queryTimeoutSeconds = int.Parse(configuration["QuerySettings:TimeoutSeconds"] ?? "30");
        }

        public async Task<int> GetProductCountAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(*) FROM Master1 WHERE MasterType = 6";
                var count = await connection.QuerySingleAsync<int>(sql, commandTimeout: _queryTimeoutSeconds);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product count from database");
                throw;
            }
        }

        public async Task<IEnumerable<ProductData>> GetProductDataAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Optimized query with explicit column selection and potential index hints
                var sql = @"
                    WITH StockData AS (
                        SELECT
                            MasterCode1 AS ItemID,
                            SUM(Value1) AS TotalAvailableStock
                        FROM
                            dbo.Tran2
                        WHERE MasterCode1 IN (SELECT Code FROM Master1 WHERE MasterType = 6) -- Pre-filter for efficiency
                        GROUP BY
                            MasterCode1
                    )
                    SELECT
                        M.Code,
                        M.Name AS ItemName,
                        M.PrintName AS PrintName,
                        M.D3 AS SalePrice,
                        M.D4 AS CostPrice,
                        ISNULL(S.TotalAvailableStock, 0) AS TotalAvailableStock,
                        GETDATE() AS LastModified -- Since we don't have actual modification times
                    FROM
                        Master1 M
                    LEFT JOIN
                        StockData S ON M.Code = S.ItemID
                    WHERE
                        M.MasterType = 6
                    ORDER BY M.Code"; // Order by primary key for consistent results

                var result = await connection.QueryAsync<ProductData>(sql, commandTimeout: _queryTimeoutSeconds);
                return result;
            }
            catch (SqlException sqlEx) when (sqlEx.Number == -2) // Timeout error
            {
                _logger.LogError(sqlEx, "Database query timed out after {TimeoutSeconds} seconds", _queryTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product data from database");
                throw;
            }
        }

        public async Task<IEnumerable<ProductData>> GetProductsByCodesAsync(IEnumerable<int> productCodes)
        {
            if (productCodes == null || !productCodes.Any()) return new List<ProductData>();

            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var codesList = productCodes.ToList();

                // Use a table-valued parameter approach or split into batches to handle large lists
                // For now, we'll use string concatenation with safety checks
                var parameters = new DynamicParameters();
                parameters.Add("@Codes", string.Join(",", codesList.Take(10000))); // Limit batch size

                var sql = @"
                    WITH StockData AS (
                        SELECT
                            MasterCode1 AS ItemID,
                            SUM(Value1) AS TotalAvailableStock
                        FROM
                            dbo.Tran2
                        WHERE MasterCode1 IN (SELECT Value FROM STRING_SPLIT(@Codes, ','))
                        GROUP BY
                            MasterCode1
                    )
                    SELECT
                        M.Code,
                        M.Name AS ItemName,
                        M.PrintName AS PrintName,
                        M.D3 AS SalePrice,
                        M.D4 AS CostPrice,
                        ISNULL(S.TotalAvailableStock, 0) AS TotalAvailableStock,
                        GETDATE() AS LastModified
                    FROM
                        Master1 M
                    LEFT JOIN StockData S ON M.Code = S.ItemID
                    WHERE M.MasterType = 6 AND M.Code IN (SELECT Value FROM STRING_SPLIT(@Codes, ','))
                    ORDER BY M.Code";

                var result = await connection.QueryAsync<ProductData>(sql, parameters, commandTimeout: _queryTimeoutSeconds);
                return result;
            }
            catch (SqlException sqlEx) when (sqlEx.Number == -2) // Timeout error
            {
                _logger.LogError(sqlEx, "Database query timed out after {TimeoutSeconds} seconds", _queryTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products by codes from database");
                throw;
            }
        }

        public async Task<IEnumerable<ProductData>> GetProductDataIncrementalAsync(DateTime lastCheckTime)
        {
            // This method now focuses on retrieving ALL products but will be used in conjunction
            // with the change tracking system to determine what has actually changed
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

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
                    ISNULL(S.TotalAvailableStock, 0) AS TotalAvailableStock,
                    GETDATE() AS LastModified
                FROM
                    Master1 M
                LEFT JOIN StockData S ON M.Code = S.ItemID
                WHERE M.MasterType = 6
                ORDER BY M.Code";

            try
            {
                var result = await connection.QueryAsync<ProductData>(sql, commandTimeout: _queryTimeoutSeconds);
                return result;
            }
            catch (SqlException sqlEx) when (sqlEx.Number == -2) // Timeout error
            {
                _logger.LogError(sqlEx, "Database query timed out after {TimeoutSeconds} seconds", _queryTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving incremental product data from database");
                throw;
            }
        }
    }
}