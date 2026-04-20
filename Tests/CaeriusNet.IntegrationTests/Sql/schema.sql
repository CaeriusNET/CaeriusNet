-- Schema bootstrap for CaeriusNet integration tests.
-- All objects live in [dbo] for simplicity; tests truncate Widgets between runs to stay
-- order-deterministic across the xunit non-parallel collection.

IF
OBJECT_ID(N'dbo.WidgetTvp', N'TT') IS NOT NULL
DROP TYPE dbo.WidgetTvp;
IF
OBJECT_ID(N'dbo.usp_InsertWidget', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_InsertWidget;
IF
OBJECT_ID(N'dbo.usp_BulkInsertWidgets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_BulkInsertWidgets;
IF
OBJECT_ID(N'dbo.usp_GetWidgetById', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetWidgetById;
IF
OBJECT_ID(N'dbo.usp_ListWidgets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_ListWidgets;
IF
OBJECT_ID(N'dbo.usp_CountWidgets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_CountWidgets;
IF
OBJECT_ID(N'dbo.usp_DeleteWidget', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_DeleteWidget;
IF
OBJECT_ID(N'dbo.usp_LongRunning', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_LongRunning;
IF
OBJECT_ID(N'dbo.usp_GetSessionIsolationLevel', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetSessionIsolationLevel;
IF
OBJECT_ID(N'dbo.usp_RaiseTestError', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_RaiseTestError;
IF
OBJECT_ID(N'dbo.usp_GetWidgetsAndCount', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetWidgetsAndCount;
IF
OBJECT_ID(N'dbo.usp_GetWidgetsCountAndFirst', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetWidgetsCountAndFirst;
IF
OBJECT_ID(N'dbo.usp_GetWidgetsFourSets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetWidgetsFourSets;
IF
OBJECT_ID(N'dbo.Widgets', N'U') IS NOT NULL
DROP TABLE dbo.Widgets;

CREATE TABLE dbo.Widgets
(
    Id        INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
    Name      NVARCHAR(100)   NOT NULL,
    Quantity  INT NOT NULL,
    CreatedAt DATETIME2(3)    NOT NULL CONSTRAINT DF_Widgets_CreatedAt DEFAULT SYSUTCDATETIME()
);
GO

CREATE TYPE dbo.WidgetTvp AS TABLE
    (
    Name NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL
    );
GO

CREATE PROCEDURE dbo.usp_InsertWidget @Name     NVARCHAR(100),
    @Quantity INT
AS
BEGIN
    SET
NOCOUNT ON;
INSERT INTO dbo.Widgets (Name, Quantity)
VALUES (@Name, @Quantity);
SELECT CAST(SCOPE_IDENTITY() AS INT);
END;
GO

CREATE PROCEDURE dbo.usp_BulkInsertWidgets @Items dbo.WidgetTvp READONLY
AS
BEGIN
    SET
NOCOUNT ON;
INSERT INTO dbo.Widgets (Name, Quantity)
SELECT Name, Quantity
FROM @Items;
SELECT @@ROWCOUNT;
END;
GO

CREATE PROCEDURE dbo.usp_GetWidgetById @Id INT
AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
WHERE Id = @Id;
END;
GO

CREATE PROCEDURE dbo.usp_ListWidgets
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
END;
GO

CREATE PROCEDURE dbo.usp_CountWidgets
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT COUNT_BIG(*)
FROM dbo.Widgets;
END;
GO

CREATE PROCEDURE dbo.usp_DeleteWidget @Id INT
AS
BEGIN
    SET
NOCOUNT ON;
DELETE
FROM dbo.Widgets
WHERE Id = @Id;
END;
GO

CREATE PROCEDURE dbo.usp_LongRunning @Seconds INT = 5
AS
BEGIN
    SET
NOCOUNT ON;
    DECLARE
@delay CHAR(8) = CONVERT(CHAR(8), DATEADD(SECOND, @Seconds, 0), 108);
    WAITFOR
DELAY @delay;
SELECT 1;
END;
GO

CREATE PROCEDURE dbo.usp_GetSessionIsolationLevel
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT CAST(transaction_isolation_level AS SMALLINT)
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;
END;
GO

-- Surfaces a server-side error (severity 16) so that exception-wrapping behaviour
-- can be exercised end-to-end without depending on connectivity / timing failures.
CREATE PROCEDURE dbo.usp_RaiseTestError
    AS
BEGIN
    SET
NOCOUNT ON;
    RAISERROR
(N'Caerius integration test forced error.', 16, 1);
END;
GO

-- Two-result-set sproc used to validate the multi-result-set query helpers.
CREATE PROCEDURE dbo.usp_GetWidgetsAndCount
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
SELECT COUNT_BIG(*) AS Total
FROM dbo.Widgets;
END;
GO

-- Three-result-set sproc: list, count, first row.
CREATE PROCEDURE dbo.usp_GetWidgetsCountAndFirst
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
SELECT COUNT_BIG(*) AS Total
FROM dbo.Widgets;
SELECT TOP(1) Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
END;
GO

-- Four-result-set sproc: list, count, first row, last row.
CREATE PROCEDURE dbo.usp_GetWidgetsFourSets
    AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
SELECT COUNT_BIG(*) AS Total
FROM dbo.Widgets;
SELECT TOP(1) Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id;
SELECT TOP(1) Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
ORDER BY Id DESC;
END;
GO
