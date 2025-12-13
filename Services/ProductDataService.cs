using System.Data.SqlClient;
using WatcherService.Models;
using Dapper;

namespace WatcherService.Services
{
    public interface IProductDataService
    {
        Task<IEnumerable<ProductData>> GetProductDataAsync();
        Task<IEnumerable<ProductData>> GetProductDataIncrementalAsync(DateTime lastCheckTime);
        Task<IEnumerable<ProductData>> GetRecentlyModifiedProductsAsync(int daysToInclude);
        Task<int> GetProductCountAsync();
        Task<IEnumerable<ProductData>> GetProductsByCodesAsync(IEnumerable<int> productCodes);
    }

    public class ProductDataService : IProductDataService
    {
        private readonly string _connectionString;
        private readonly ILogger<ProductDataService> _logger;
        private readonly int _queryTimeoutSeconds;
        private readonly int _transactionDaysToInclude;
        private readonly bool _useTimeFiltering;
        private readonly bool _useRecentlyModifiedOnly;
        private readonly bool _useCreationDateFiltering;
        private readonly string _dateColumnName;
        private readonly string _creationDateColumnName;

        public ProductDataService(IConfiguration configuration, ILogger<ProductDataService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection");
            _logger = logger;
            // Default to 30 seconds, but allow configuration override
            _queryTimeoutSeconds = int.Parse(configuration["QuerySettings:TimeoutSeconds"] ?? "30");
            // Default to 90 days, but allow configuration override for transaction time window
            _transactionDaysToInclude = int.Parse(configuration["TransactionSettings:DaysToInclude"] ?? "90");
            // Default to true, but allow configuration override for using time filtering
            _useTimeFiltering = bool.Parse(configuration["TransactionSettings:UseTimeFiltering"] ?? "true");
            // Default to false, but allow configuration override for fetching only recently modified products
            _useRecentlyModifiedOnly = bool.Parse(configuration["TransactionSettings:UseRecentlyModifiedOnly"] ?? "false");
            // Default to false, but allow configuration override for including creation date filtering
            _useCreationDateFiltering = bool.Parse(configuration["TransactionSettings:UseCreationDateFiltering"] ?? "false");
            // Default to TranDate, but allow configuration override for the transaction date column name
            _dateColumnName = configuration["TransactionSettings:DateColumnName"] ?? "TranDate";
            // Default to CreatedDate, but allow configuration override for the creation date column name
            _creationDateColumnName = configuration["TransactionSettings:CreationDateColumnName"] ?? "CreatedDate";
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

                // Build the SQL query based on whether time filtering is enabled
                string whereClause;
                object parameters;

                if (_useTimeFiltering)
                {
                    var cutoffDate = DateTime.Now.AddDays(-_transactionDaysToInclude);
                    whereClause = $"AND [{_dateColumnName}] >= @CutoffDate";  // Use square brackets for column name safety
                    parameters = new { CutoffDate = cutoffDate };
                }
                else
                {
                    whereClause = "";
                    parameters = new { };
                }

                // Optimized query with explicit column selection and potential index hints
                var sql = $@"
                    WITH StockData AS (
                        SELECT
                            MasterCode1 AS ItemID,
                            SUM(Value1) AS TotalAvailableStock
                        FROM
                            dbo.Tran2
                        WHERE MasterCode1 IN (SELECT Code FROM Master1 WHERE MasterType = 6) -- Pre-filter for efficiency
                          {whereClause}  -- Only include recent transactions if filtering is enabled
                        GROUP BY
                            MasterCode1
                    )
                    SELECT
                        M.Code,
                        M.Name AS ItemName,
                        M.PrintName AS PrintName,
                        M.D3 AS SalePrice,
                        M.D4 AS CostPrice,
                        CASE
                            WHEN ISNULL(S.TotalAvailableStock, 0) < 0 THEN 0
                            ELSE ISNULL(S.TotalAvailableStock, 0)
                        END AS TotalAvailableStock,
                        GETDATE() AS LastModified -- Since we don't have actual modification times
                    FROM
                        Master1 M
                    LEFT JOIN
                        StockData S ON M.Code = S.ItemID
                    WHERE
                        M.MasterType = 6
                    ORDER BY M.Code"; // Order by primary key for consistent results

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

                // Build the SQL query based on whether time filtering is enabled
                string whereClause;
                var parameters = new DynamicParameters();
                parameters.Add("@Codes", string.Join(",", codesList.Take(10000))); // Limit batch size

                if (_useTimeFiltering)
                {
                    var cutoffDate = DateTime.Now.AddDays(-_transactionDaysToInclude);
                    whereClause = $"AND [{_dateColumnName}] >= @CutoffDate";  // Use square brackets for column name safety
                    parameters.Add("@CutoffDate", cutoffDate);
                }
                else
                {
                    whereClause = "";
                }

                var sql = $@"
                    WITH StockData AS (
                        SELECT
                            MasterCode1 AS ItemID,
                            SUM(Value1) AS TotalAvailableStock
                        FROM
                            dbo.Tran2
                        WHERE MasterCode1 IN (SELECT Value FROM STRING_SPLIT(@Codes, ','))
                          {whereClause}  -- Only include recent transactions if filtering is enabled
                        GROUP BY
                            MasterCode1
                    )
                    SELECT
                        M.Code,
                        M.Name AS ItemName,
                        M.PrintName AS PrintName,
                        M.D3 AS SalePrice,
                        M.D4 AS CostPrice,
                        CASE
                            WHEN ISNULL(S.TotalAvailableStock, 0) < 0 THEN 0
                            ELSE ISNULL(S.TotalAvailableStock, 0)
                        END AS TotalAvailableStock,
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

            // Build the SQL query based on whether time filtering is enabled
            string whereClause;
            object parameters;

            if (_useTimeFiltering)
            {
                var cutoffDate = DateTime.Now.AddDays(-_transactionDaysToInclude);
                whereClause = $"AND [{_dateColumnName}] >= @CutoffDate";  // Use square brackets for column name safety
                parameters = new { CutoffDate = cutoffDate };
            }
            else
            {
                whereClause = "";
                parameters = new { };
            }

            var sql = $@"
                WITH StockData AS (
                    SELECT
                        MasterCode1 AS ItemID,
                        SUM(Value1) AS TotalAvailableStock
                    FROM
                        dbo.Tran2
                    WHERE
                        MasterCode1 IN (SELECT Code FROM Master1 WHERE MasterType = 6)
                      {whereClause}  -- Only include recent transactions if filtering is enabled
                    GROUP BY
                        MasterCode1
                )
                SELECT
                    M.Code,
                    M.Name AS ItemName,
                    M.PrintName AS PrintName,
                    M.D3 AS SalePrice,
                    M.D4 AS CostPrice,
                    CASE
                        WHEN ISNULL(S.TotalAvailableStock, 0) < 0 THEN 0
                        ELSE ISNULL(S.TotalAvailableStock, 0)
                    END AS TotalAvailableStock,
                    GETDATE() AS LastModified
                FROM
                    Master1 M
                LEFT JOIN StockData S ON M.Code = S.ItemID
                WHERE M.MasterType = 6
                ORDER BY M.Code";

            try
            {
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
                _logger.LogError(ex, "Error retrieving incremental product data from database");
                throw;
            }
        }

        public async Task<IEnumerable<ProductData>> GetRecentlyModifiedProductsAsync(int daysToInclude)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                if (_useTimeFiltering)
                {
                    var cutoffDate = DateTime.Now.AddDays(-daysToInclude);
                    var parameters = new { CutoffDate = cutoffDate };

                    // First, get the list of recently transacted product codes (more efficient approach)
                    var recentProductCodesSql = $@"
                        SELECT DISTINCT t.MasterCode1
                        FROM dbo.Tran2 t
                        INNER JOIN Master1 m ON m.Code = t.MasterCode1
                        WHERE m.MasterType = 6
                          AND t.[{_dateColumnName}] >= @CutoffDate";

                    var recentProductCodes = (await connection.QueryAsync<int>(recentProductCodesSql, parameters)).ToList();

                    // If creation date filtering is enabled, get those too
                    if (_useCreationDateFiltering)
                    {
                        var createdProductCodesSql = $@"
                            SELECT Code
                            FROM Master1
                            WHERE MasterType = 6
                              AND [{_creationDateColumnName}] >= @CutoffDate";

                        try
                        {
                            var createdProductCodes = (await connection.QueryAsync<int>(createdProductCodesSql, parameters)).ToList();
                            recentProductCodes.AddRange(createdProductCodes);
                            // Remove duplicates
                            recentProductCodes = recentProductCodes.Distinct().ToList();
                        }
                        catch (SqlException ex) when (ex.Number == 207) // Invalid column name
                        {
                            _logger.LogWarning("Creation date column not found: {_creationDateColumnName}", _creationDateColumnName);
                        }
                    }

                    // If no product codes found, return empty list
                    if (!recentProductCodes.Any())
                    {
                        return new List<ProductData>();
                    }

                    // Split into batches to handle SQL Server parameter limit (2100 max)
                    var productCodes = recentProductCodes.Take(10000).ToList(); // Limit to avoid SQL limitations
                    const int batchSize = 2000; // Leave room for other parameters
                    var allStockData = new List<dynamic>();
                    var allProducts = new List<ProductData>();

                    // Process stock data in batches
                    for (int i = 0; i < productCodes.Count; i += batchSize)
                    {
                        var batch = productCodes.Skip(i).Take(batchSize).ToList();
                        var codesPlaceholder = string.Join(",", Enumerable.Range(0, batch.Count).Select(j => $"@p{j}"));

                        var stockParameters = new DynamicParameters();
                        for (int j = 0; j < batch.Count; j++)
                        {
                            stockParameters.Add($"p{j}", batch[j]);
                        }
                        stockParameters.Add("CutoffDate", cutoffDate);

                        var stockSql = $@"
                            SELECT
                                MasterCode1 AS ItemID,
                                SUM(Value1) AS TotalAvailableStock
                            FROM
                                dbo.Tran2
                            WHERE
                                MasterCode1 IN ({codesPlaceholder})
                                AND [{_dateColumnName}] >= @CutoffDate
                            GROUP BY
                                MasterCode1";

                        var batchStockData = await connection.QueryAsync<dynamic>(stockSql, stockParameters);
                        allStockData.AddRange(batchStockData);
                    }

                    // Create a dictionary for fast lookup
                    var stockDict = allStockData.ToDictionary(
                        s => (int)s.ItemID,
                        s => Math.Max(0, (int)s.TotalAvailableStock) // Apply the min 0 rule here
                    );

                    // Process product details in batches
                    for (int i = 0; i < productCodes.Count; i += batchSize)
                    {
                        var batch = productCodes.Skip(i).Take(batchSize).ToList();
                        var codesPlaceholder = string.Join(",", Enumerable.Range(0, batch.Count).Select(j => $"@p{j}"));

                        var productParameters = new DynamicParameters();
                        for (int j = 0; j < batch.Count; j++)
                        {
                            productParameters.Add($"p{j}", batch[j]);
                        }

                        var productsSql = $@"
                            SELECT
                                Code,
                                Name AS ItemName,
                                PrintName,
                                D3 AS SalePrice,
                                D4 AS CostPrice
                            FROM
                                Master1
                            WHERE
                                MasterType = 6
                                AND Code IN ({codesPlaceholder})
                            ORDER BY Code";

                        var batchProducts = await connection.QueryAsync<ProductData>(productsSql, productParameters);
                        allProducts.AddRange(batchProducts);
                    }

                    // Combine product info with stock info
                    var result = allProducts.Select(p => new ProductData
                    {
                        Code = p.Code,
                        ItemName = p.ItemName,
                        PrintName = p.PrintName,
                        SalePrice = p.SalePrice,
                        CostPrice = p.CostPrice,
                        TotalAvailableStock = stockDict.ContainsKey(p.Code) ? stockDict[p.Code] : 0,
                        LastModified = DateTime.Now
                    }).ToList();

                    return result;
                }
                else
                {
                    // Without time filtering - get all products (but with basic stock calculation)
                    var sql = @"
                        SELECT
                            Code,
                            Name AS ItemName,
                            PrintName,
                            D3 AS SalePrice,
                            D4 AS CostPrice
                        FROM
                            Master1
                        WHERE
                            MasterType = 6
                        ORDER BY Code";

                    var products = await connection.QueryAsync<ProductData>(sql, commandTimeout: _queryTimeoutSeconds);

                    // For each product, calculate stock separately if not using time filtering
                    var result = new List<ProductData>();
                    foreach (var product in products)
                    {
                        result.Add(new ProductData
                        {
                            Code = product.Code,
                            ItemName = product.ItemName,
                            PrintName = product.PrintName,
                            SalePrice = product.SalePrice,
                            CostPrice = product.CostPrice,
                            TotalAvailableStock = 0, // Would require separate query to calculate full stock
                            LastModified = DateTime.Now
                        });
                    }

                    return result;
                }
            }
            catch (SqlException sqlEx) when (sqlEx.Number == -2) // Timeout error
            {
                _logger.LogError(sqlEx, "Database query timed out after {TimeoutSeconds} seconds", _queryTimeoutSeconds);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recently modified products from database");
                throw;
            }
        }
    }
}