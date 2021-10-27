SELECT c.FirstName,
       c.LastName AS ln,
       e.JobTitle,
       a.AddressLine1,
       a.City AS ct,
       a.PostalCode
INTO dbo.EmployeeAddresses
FROM Person.Person AS c,
     HumanResources.Employee AS e
                         JOIN Person.Address AS a
                           ON e.ModifiedDate = a.ModifiedDate AND
                              e.rowguid = a.rowguid AND
                              (e.ModifiedDate IS NOT NULL OR
                               a.rowguid IS NOT NULL) AND
                              e.JobTitle IS NULL
                         JOIN Person.Person p
                           ON e.BusinessEntityID = p.BusinessEntityID
WHERE a.AddressLine1 IS NOT NULL AND
      a.AddressLine2 IS NOT NULL AND
      (a.PostalCode IS NOT NULL OR
       a.PostalCode IS NOT NULL) AND
      e.JobTitle IS NULL
GROUP BY c.FirstName, c.LastName, e.JobTitle, a.AddressLine1, a.City, a.PostalCode
HAVING a.City = 'New York' AND
       a.PostalCode IS NOT NULL AND
       (a.AddressLine1 IS NOT NULL OR
        a.AddressLine2 IS NOT NULL) AND
       e.JobTitle IS NULL
ORDER BY ln ASC, ct, a.AddressLine1 DESC;
GO
