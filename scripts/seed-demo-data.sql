-- Adds ~300 SYNTHETIC paid orders over the last 14 days (lunch and dinner rushes)
-- so the /reports endpoints have something to show. Demo data only, not real sales.
-- Run:  docker compose exec -T db psql -U pos -d restaurant < scripts/seed-demo-data.sql
DO $$
DECLARE
  names  text[]   := ARRAY['Burger','Fries','Pizza','Pasta','Salad','Cola','Coffee','Brownie'];
  prices bigint[] := ARRAY[1000,500,1200,1100,700,300,350,600];
  hours  int[]    := ARRAY[11,12,12,13,13,14,18,19,19,20,20,21];
  oid uuid; paid timestamptz; n int; j int; k int; s int; idx int;
BEGIN
  FOR n IN 1..300 LOOP
    oid := gen_random_uuid();
    paid := ((date_trunc('day', now() AT TIME ZONE 'Asia/Kolkata') - ((1 + floor(random()*14))::int || ' days')::interval)
             + (hours[1 + floor(random()*12)::int] * interval '1 hour')
             + (floor(random()*60)::int * interval '1 minute')) AT TIME ZONE 'Asia/Kolkata';
    INSERT INTO orders (id, table_number, status, tax_rate_basis_points, discount_percent,
                        created_at, sent_at, paid_at, paid_with, tendered_cents, change_cents)
    VALUES (oid, 1 + floor(random()*12)::int, 'Paid', 500,
            CASE WHEN random() < 0.15 THEN 10 ELSE 0 END,
            paid - interval '40 minutes', paid - interval '30 minutes', paid,
            CASE WHEN random() < 0.5 THEN 'Card' ELSE 'Cash' END, 0, 0);
    k := 1 + floor(random()*4)::int;
    s := floor(random()*8)::int;
    FOR j IN 1..k LOOP
      idx := ((s + j*3) % 8) + 1;
      INSERT INTO order_lines (id, order_id, menu_item_id, item_name, unit_price_cents, quantity)
      VALUES (gen_random_uuid(), oid,
              COALESCE((SELECT id FROM menu_items WHERE name = names[idx] LIMIT 1), gen_random_uuid()),
              names[idx], prices[idx], 1 + floor(random()*3)::int);
    END LOOP;
  END LOOP;
END $$;

UPDATE orders o
SET tendered_cents = CASE WHEN o.paid_with = 'Card' THEN t.total ELSE ((t.total + 499) / 500) * 500 END,
    change_cents   = CASE WHEN o.paid_with = 'Card' THEN 0 ELSE ((t.total + 499) / 500) * 500 - t.total END
FROM (
  SELECT o2.id,
         s.sub - (s.sub * o2.discount_percent + 50) / 100
         + ((s.sub - (s.sub * o2.discount_percent + 50) / 100) * o2.tax_rate_basis_points + 5000) / 10000 AS total
  FROM orders o2
  JOIN (SELECT order_id, SUM(unit_price_cents * quantity) AS sub FROM order_lines GROUP BY order_id) s ON s.order_id = o2.id
) t
WHERE o.id = t.id AND o.tendered_cents = 0;
