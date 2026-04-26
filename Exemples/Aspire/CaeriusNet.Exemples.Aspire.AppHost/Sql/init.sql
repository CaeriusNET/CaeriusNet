/*
================================================================================
  CaeriusNet examples — schema bootstrap.
================================================================================
  This script creates everything the Default and Aspire console examples need:

    Schemas      : Users, Types
    Tables       : Users.Users, Users.Orders
    TVP types    : Types.tvp_Int, Types.tvp_Guid, Types.tvp_IntGuid
    Procedures   : Users.usp_*  (read / write, single + multi result-sets,
                                 TVP + multi-RS, transactional create with
                                 SQL-side rollback)

  Idempotent: every object is dropped before being (re)created. Safe to run
  on a fresh database **or** to refresh an existing one.

  All inserts are intentionally limited to ~10 rows so the examples stay
  readable when displayed on the console.
================================================================================
*/

SET
NOCOUNT ON;
SET
XACT_ABORT ON;
GO

------------------------------------------------------------
-- 1. Drop dependent objects
------------------------------------------------------------
IF OBJECT_ID(N'Users.usp_Get_Dashboard', N'P')              IS NOT NULL
DROP PROCEDURE Users.usp_Get_Dashboard;
IF
OBJECT_ID(N'Users.usp_Get_Users_With_Orders_By_Tvp', N'P') IS NOT NULL
DROP PROCEDURE Users.usp_Get_Users_With_Orders_By_Tvp;
IF
OBJECT_ID(N'Users.usp_Create_User_Tx_Safe', N'P')        IS NOT NULL
DROP PROCEDURE Users.usp_Create_User_Tx_Safe;
IF
OBJECT_ID(N'Users.usp_Create_Order', N'P')               IS NOT NULL
DROP PROCEDURE Users.usp_Create_Order;
IF
OBJECT_ID(N'Users.usp_Create_User', N'P')                IS NOT NULL
DROP PROCEDURE Users.usp_Create_User;
IF
OBJECT_ID(N'Users.usp_Get_Users_From_TvpInt', N'P')      IS NOT NULL
DROP PROCEDURE Users.usp_Get_Users_From_TvpInt;
IF
OBJECT_ID(N'Users.usp_Get_Users_From_TvpGuid', N'P')     IS NOT NULL
DROP PROCEDURE Users.usp_Get_Users_From_TvpGuid;
IF
OBJECT_ID(N'Users.usp_Get_Users_From_TvpIntGuid', N'P')  IS NOT NULL
DROP PROCEDURE Users.usp_Get_Users_From_TvpIntGuid;
IF
OBJECT_ID(N'Users.usp_Get_All_Users', N'P')              IS NOT NULL
DROP PROCEDURE Users.usp_Get_All_Users;

IF
OBJECT_ID(N'Users.Orders', N'U') IS NOT NULL
DROP TABLE Users.Orders;
IF
OBJECT_ID(N'Users.Users',  N'U') IS NOT NULL
DROP TABLE Users.Users;

IF
TYPE_ID(N'Types.tvp_IntGuid') IS NOT NULL
DROP TYPE Types.tvp_IntGuid;
IF
TYPE_ID(N'Types.tvp_Guid')    IS NOT NULL
DROP TYPE Types.tvp_Guid;
IF
TYPE_ID(N'Types.tvp_Int')     IS NOT NULL
DROP TYPE Types.tvp_Int;
GO

------------------------------------------------------------
-- 2. Schemas
------------------------------------------------------------
IF SCHEMA_ID(N'Users') IS NULL EXEC(N'CREATE SCHEMA Users AUTHORIZATION dbo;');
IF
SCHEMA_ID(N'Types') IS NULL EXEC(N'CREATE SCHEMA Types AUTHORIZATION dbo;');
GO

------------------------------------------------------------
-- 3. Tables
------------------------------------------------------------
CREATE TABLE Users.Users
(
    UserId    INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Users_Users PRIMARY KEY,
    UserGuid  UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Users_UserGuid DEFAULT NEWID(),
    UserName  NVARCHAR(64)    NOT NULL,
    CreatedAt DATETIME2(3)    NOT NULL CONSTRAINT DF_Users_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Users_Users_UserGuid UNIQUE (UserGuid),
    CONSTRAINT UQ_Users_Users_UserName UNIQUE (UserName)
);
GO

CREATE TABLE Users.Orders
(
    OrderId   INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Users_Orders PRIMARY KEY,
    UserId    INT            NOT NULL
        CONSTRAINT FK_Users_Orders_Users REFERENCES Users.Users (UserId),
    Label     NVARCHAR(64) NOT NULL,
    Amount    DECIMAL(10, 2) NOT NULL,
    CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_Users_Orders_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

------------------------------------------------------------
-- 4. Table-Valued Parameter types
--    Names match the [GenerateTvp] attributes of the Commons project.
------------------------------------------------------------
CREATE TYPE Types.tvp_Int AS TABLE
    (
    UserId INT NOT NULL
    );
GO

CREATE TYPE Types.tvp_Guid AS TABLE
    (
    UserGuid UNIQUEIDENTIFIER NOT NULL
    );
GO

CREATE TYPE Types.tvp_IntGuid AS TABLE
    (
    UserId INT NOT NULL,
    UserGuid UNIQUEIDENTIFIER NOT NULL
    );
GO

------------------------------------------------------------
-- 5. Seed data — small enough to fit on a console screen.
------------------------------------------------------------
INSERT INTO Users.Users (UserGuid, UserName)
VALUES
    ('11111111-1111-1111-1111-111111111111', N'alice'),
    ('22222222-2222-2222-2222-222222222222', N'bob'),
    ('33333333-3333-3333-3333-333333333333', N'carol'),
    ('44444444-4444-4444-4444-444444444444', N'dave'),
    ('55555555-5555-5555-5555-555555555555', N'erin');

INSERT INTO Users.Orders (UserId, Label, Amount)
VALUES (1, N'Coffee', 3.50),
       (1, N'Croissant', 2.20),
       (2, N'Notebook', 12.90),
       (3, N'Pen', 1.75),
       (3, N'Stickers', 4.00),
       (5, N'Tea', 2.10);
GO

------------------------------------------------------------
-- 6. Single-result-set procedures
------------------------------------------------------------
CREATE PROCEDURE Users.usp_Get_All_Users
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT UserId, UserGuid
FROM Users.Users
ORDER BY UserId;
END;
GO

CREATE PROCEDURE Users.usp_Get_Users_From_TvpInt @tvp Types.tvp_Int READONLY
AS
BEGIN
    SET
NOCOUNT ON;
SELECT u.UserId, u.UserGuid
FROM Users.Users AS u
         INNER JOIN @tvp AS t ON t.UserId = u.UserId
ORDER BY u.UserId;
END;
GO

CREATE PROCEDURE Users.usp_Get_Users_From_TvpGuid @tvp Types.tvp_Guid READONLY
AS
BEGIN
    SET
NOCOUNT ON;
SELECT u.UserId, u.UserGuid
FROM Users.Users AS u
         INNER JOIN @tvp AS t ON t.UserGuid = u.UserGuid
ORDER BY u.UserId;
END;
GO

CREATE PROCEDURE Users.usp_Get_Users_From_TvpIntGuid @tvp Types.tvp_IntGuid READONLY
AS
BEGIN
    SET
NOCOUNT ON;
SELECT u.UserId, u.UserGuid
FROM Users.Users AS u
         INNER JOIN @tvp AS t ON t.UserId = u.UserId AND t.UserGuid = u.UserGuid
ORDER BY u.UserId;
END;
GO

------------------------------------------------------------
-- 7. Write procedures
------------------------------------------------------------
CREATE PROCEDURE Users.usp_Create_User @UserName NVARCHAR(64) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
    DECLARE
@name NVARCHAR(64) = COALESCE(@UserName, CONCAT(N'demo-', NEWID()));

INSERT INTO Users.Users (UserName)
VALUES (@name);
SELECT CAST(SCOPE_IDENTITY() AS INT) AS UserId;
END;
GO

CREATE PROCEDURE Users.usp_Create_Order @UserId INT,
    @Label  NVARCHAR(64),
    @Amount DECIMAL(10, 2)
AS
BEGIN
    SET
NOCOUNT ON;
INSERT INTO Users.Orders (UserId, Label, Amount)
VALUES (@UserId, @Label, @Amount);
SELECT CAST(SCOPE_IDENTITY() AS INT) AS OrderId;
END;
GO

------------------------------------------------------------
-- 8. Multi-result-set procedure
--    Returns three sets: users / orders / per-user totals.
--    Demonstrates the "caerius.resultset.multi = true" telemetry tag.
------------------------------------------------------------
CREATE PROCEDURE Users.usp_Get_Dashboard
    AS
BEGIN
    SET
NOCOUNT ON;

    -- Set #1: users
SELECT UserId, UserGuid
FROM Users.Users
ORDER BY UserId;

-- Set #2: orders
SELECT OrderId, UserId, Label, Amount, CreatedAt
FROM Users.Orders
ORDER BY OrderId;

-- Set #3: per-user totals
SELECT u.UserId,
       u.UserName,
       COUNT(o.OrderId)           AS OrdersCount,
       COALESCE(SUM(o.Amount), 0) AS TotalAmount
FROM Users.Users AS u
         LEFT JOIN Users.Orders AS o ON o.UserId = u.UserId
GROUP BY u.UserId, u.UserName
ORDER BY u.UserId;
END;
GO

------------------------------------------------------------
-- 9. TVP + multi-result-set in a single call.
--    Returns selected users (set #1) and their orders (set #2).
------------------------------------------------------------
CREATE PROCEDURE Users.usp_Get_Users_With_Orders_By_Tvp @tvp Types.tvp_Int READONLY
AS
BEGIN
    SET
NOCOUNT ON;

    -- Set #1: matching users
SELECT u.UserId, u.UserGuid
FROM Users.Users AS u
         INNER JOIN @tvp AS t ON t.UserId = u.UserId
ORDER BY u.UserId;

-- Set #2: their orders
SELECT o.OrderId, o.UserId, o.Label, o.Amount, o.CreatedAt
FROM Users.Orders AS o
         INNER JOIN @tvp AS t ON t.UserId = o.UserId
ORDER BY o.OrderId;
END;
GO

------------------------------------------------------------
-- 10. Transaction-friendly procedure with SQL-side rollback.
--     Wraps the work in BEGIN TRY / BEGIN CATCH and re-raises so the
--     caller sees a CaeriusNetSqlException — mirroring the C#-side
--     rollback example in UsersRepository.
------------------------------------------------------------
CREATE PROCEDURE Users.usp_Create_User_Tx_Safe @UserName NVARCHAR(64),
    @ForceFailure BIT = 0
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

BEGIN TRY
BEGIN
TRANSACTION;

INSERT INTO Users.Users (UserName)
VALUES (@UserName);
DECLARE
@newUserId INT = CAST(SCOPE_IDENTITY() AS INT);

        IF
@ForceFailure = 1
            THROW 50001, N'Forced failure inside usp_Create_User_Tx_Safe — rolling back.', 1;

INSERT INTO Users.Orders (UserId, Label, Amount)
VALUES (@newUserId, N'Welcome bonus', 0.00);

COMMIT TRANSACTION;

SELECT @newUserId AS UserId;
END TRY
BEGIN CATCH
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
END CATCH;
END;
GO
