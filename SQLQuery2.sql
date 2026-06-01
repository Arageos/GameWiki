USE GameWikiDb;
GO
SELECT
    dp.name                             AS [Użytkownik],
    sl.name                             AS [Login],
    sl.password_hash                    AS [Hash hasła],
    STRING_AGG(rp.name, ', ')           AS [Przypisane role]
FROM sys.database_principals dp
JOIN sys.sql_logins sl
    ON dp.sid = sl.sid
LEFT JOIN sys.database_role_members drm
    ON drm.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals rp
    ON rp.principal_id = drm.role_principal_id
WHERE dp.name IN (
    'AdminUser','AppIdentityUser',
    'SzymonUser','LukaszUser','MariuszUser'
)
GROUP BY dp.name, sl.name, sl.password_hash
ORDER BY dp.name;