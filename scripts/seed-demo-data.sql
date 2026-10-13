-- Adds ~300 SYNTHETIC paid orders over the last 14 days (lunch and dinner rushes)
-- so the /reports endpoints have something to show. Demo data only, not real sales.
-- Run (T-SQL, SQL Server):
--   docker compose cp scripts/seed-demo-data.sql db:/tmp/seed.sql
--   docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Pos_Strong_Pw1!" -C -d restaurant -i /tmp/seed.sql
SET NOCOUNT ON;

DECLARE @items TABLE (idx int PRIMARY KEY, name nvarchar(120), price bigint);
INSERT INTO @items VALUES
  (1, N'Burger', 1000), (2, N'Fries', 500), (3, N'Pizza', 1200), (4, N'Pasta', 1100),
  (5, N'Salad', 700), (6, N'Cola', 300), (7, N'Coffee', 350), (8, N'Brownie', 600);

DECLARE @hours TABLE (idx int PRIMARY KEY, hr int);
INSERT INTO @hours VALUES (1,11),(2,12),(3,12),(4,13),(5,13),(6,14),(7,18),(8,19),(9,19),(10,20),(11,20),(12,21);

DECLARE @todayIst date = CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '+05:30') AS date);
DECLARE @n int = 1, @j int, @k int, @s int, @idx int, @hr int,
        @oid uniqueidentifier, @paid datetime2;

BEGIN TRANSACTION;

WHILE @n <= 300
BEGIN
  SET @oid = NEWID();

  -- ABS(CHECKSUM(NEWID())) % N gives a fresh random number 0..N-1 on every call.
  SET @idx = 1 + ABS(CHECKSUM(NEWID())) % 12;
  SELECT @hr = hr FROM @hours WHERE idx = @idx;

  -- A local (IST) time on one of the last 14 days, converted to UTC (IST = UTC + 330 minutes).
  SET @paid = DATEADD(MINUTE, (ABS(CHECKSUM(NEWID())) % 60) - 330,
                DATEADD(HOUR, @hr,
                  CAST(DATEADD(DAY, -(1 + ABS(CHECKSUM(NEWID())) % 14), @todayIst) AS datetime2)));

  INSERT INTO orders (id, table_number, status, tax_rate_basis_points, discount_percent,
                      created_at, sent_at, paid_at, paid_with, tendered_cents, change_cents)
  VALUES (@oid, 1 + ABS(CHECKSUM(NEWID())) % 12, N'Paid', 500,
          CASE WHEN ABS(CHECKSUM(NEWID())) % 100 < 15 THEN 10 ELSE 0 END,
          DATEADD(MINUTE, -40, @paid), DATEADD(MINUTE, -30, @paid), @paid,
          CASE WHEN ABS(CHECKSUM(NEWID())) % 2 = 0 THEN N'Card' ELSE N'Cash' END, 0, 0);

  SET @k = 1 + ABS(CHECKSUM(NEWID())) % 4;
  SET @s = ABS(CHECKSUM(NEWID())) % 8;
  SET @j = 1;
  WHILE @j <= @k
  BEGIN
    SET @idx = ((@s + @j * 3) % 8) + 1;
    INSERT INTO order_lines (id, order_id, menu_item_id, item_name, unit_price_cents, quantity)
    SELECT NEWID(), @oid,
           COALESCE((SELECT TOP (1) m.id FROM menu_items m WHERE m.name = i.name), NEWID()),
           i.name, i.price, 1 + ABS(CHECKSUM(NEWID())) % 3
    FROM @items i
    WHERE i.idx = @idx;
    SET @j += 1;
  END;

  SET @n += 1;
END;

-- Card pays the exact total; cash is rounded up to the next 500 cents and gets change.
UPDATE o
SET tendered_cents = CASE WHEN o.paid_with = N'Card' THEN t.total ELSE ((t.total + 499) / 500) * 500 END,
    change_cents   = CASE WHEN o.paid_with = N'Card' THEN 0 ELSE ((t.total + 499) / 500) * 500 - t.total END
FROM orders o
JOIN (
  SELECT o2.id,
         s.sub - (s.sub * o2.discount_percent + 50) / 100
         + ((s.sub - (s.sub * o2.discount_percent + 50) / 100) * o2.tax_rate_basis_points + 5000) / 10000 AS total
  FROM orders o2
  JOIN (SELECT order_id, SUM(unit_price_cents * quantity) AS sub FROM order_lines GROUP BY order_id) s
    ON s.order_id = o2.id
) t ON t.id = o.id
WHERE o.tendered_cents = 0;

COMMIT TRANSACTION;
GO
