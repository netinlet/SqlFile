SELECT Id, Name, Region
FROM Customers
WHERE Region = {{region}}
  AND Name LIKE {{namePattern}}
