-- THIS WORKS FOR HARD CODED PRODUCTS


WITH StockData AS (
    -- 1. Calculate the Available Stock from the Tran2 table
    SELECT
        MasterCode1 AS ItemID,
        SUM(Value1) AS TotalAvailableStock
    FROM
        dbo.Tran2
    WHERE
        MasterCode1 IN (1152, 1153, 1154, 1155) -- Include all four items
    GROUP BY
        MasterCode1
)

SELECT
    M.Code,
    M.Name AS "Item Name",
    M.PrintName AS "Print Name",
    M.D3 AS "Sale Price",      -- Sale Price
    M.D4 AS "Cost Price",      -- Cost Price
    S.TotalAvailableStock      -- Calculated Stock Status
FROM
    Master1 M
JOIN
    StockData S ON M.Code = S.ItemID  -- 2. Join the Master data with the Stock data
WHERE
    M.MasterType = 6;