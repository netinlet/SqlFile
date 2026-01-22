SELECT Id, Name, Region
FROM Customers
WHERE Region = {0}
  AND Name LIKE {{namePattern}}
