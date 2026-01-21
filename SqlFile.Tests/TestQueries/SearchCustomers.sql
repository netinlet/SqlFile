SELECT Id, Name, Region
FROM Customers
WHERE {{activeFilter}}
  AND {{regionFilter}}
ORDER BY {{sortColumn}}
