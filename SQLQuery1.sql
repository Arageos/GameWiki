USE master;
GO

-- ══════════════════════════════════════
-- LOGINY NA POZIOMIE SERWERA
-- ══════════════════════════════════════
CREATE LOGIN AdminLogin         WITH PASSWORD = 'Admin123!';
CREATE LOGIN AppIdentityLogin   WITH PASSWORD = 'AppIdentity123!';
CREATE LOGIN SzymonLogin        WITH PASSWORD = 'Szymon123!';
CREATE LOGIN LukaszLogin        WITH PASSWORD = 'Lukasz123!';
CREATE LOGIN MariuszLogin       WITH PASSWORD = 'Mariusz123!';
GO

USE GameWikiDb;
GO

-- ══════════════════════════════════════
-- UŻYTKOWNICY W BAZIE DANYCH
-- ══════════════════════════════════════
CREATE USER AdminUser           FOR LOGIN AdminLogin;
CREATE USER AppIdentityUser     FOR LOGIN AppIdentityLogin;
CREATE USER SzymonUser          FOR LOGIN SzymonLogin;
CREATE USER LukaszUser          FOR LOGIN LukaszLogin;
CREATE USER MariuszUser         FOR LOGIN MariuszLogin;
GO

-- ══════════════════════════════════════
-- ROLA db_procexecutor
-- ══════════════════════════════════════
CREATE ROLE db_procexecutor;
GO

GRANT EXECUTE TO db_procexecutor;
GO

-- ══════════════════════════════════════
-- PRZYPISANIE RÓL
-- ══════════════════════════════════════

-- Administrator: właściciel bazy
ALTER ROLE db_owner          ADD MEMBER AdminUser;

-- ApplicationIdentity: READ + WRITE + EXECUTE
ALTER ROLE db_datareader     ADD MEMBER AppIdentityUser;
ALTER ROLE db_datawriter     ADD MEMBER AppIdentityUser;
ALTER ROLE db_procexecutor   ADD MEMBER AppIdentityUser;

-- Deweloperzy: READ
ALTER ROLE db_datareader     ADD MEMBER SzymonUser;
ALTER ROLE db_datareader     ADD MEMBER LukaszUser;
ALTER ROLE db_datareader     ADD MEMBER MariuszUser;
GO