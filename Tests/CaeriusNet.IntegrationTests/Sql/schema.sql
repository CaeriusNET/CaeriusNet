-- Schema bootstrap for CaeriusNet integration tests.
-- All objects live in [dbo] for simplicity; tests truncate Widgets between runs to stay
-- order-deterministic across the xunit non-parallel collection.

IF
OBJECT_ID(N'dbo.usp_InsertWidget', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_InsertWidget;
IF
OBJECT_ID(N'contracts.usp_SearchWidgets', N'P') IS NOT NULL
DROP PROCEDURE contracts.usp_SearchWidgets;
IF
OBJECT_ID(N'contracts.usp_PreviewWidgetBatch', N'P') IS NOT NULL
DROP PROCEDURE contracts.usp_PreviewWidgetBatch;
IF
OBJECT_ID(N'contracts.usp_QuoteWidget', N'P') IS NOT NULL
DROP PROCEDURE contracts.usp_QuoteWidget;
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
OBJECT_ID(N'dbo.usp_GetWidgetsFiveSets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_GetWidgetsFiveSets;
IF
OBJECT_ID(N'dbo.usp_AutoContracts_SearchWidgets', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_AutoContracts_SearchWidgets;
IF
OBJECT_ID(N'dbo.usp_AutoContracts_GetWidgetById', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_AutoContracts_GetWidgetById;
IF
OBJECT_ID(N'dbo.usp_AutoContracts_PreviewWidgetBatch', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_AutoContracts_PreviewWidgetBatch;
IF
OBJECT_ID(N'dbo.usp_AutoContracts_QuoteWidget', N'P') IS NOT NULL
DROP PROCEDURE dbo.usp_AutoContracts_QuoteWidget;
IF
OBJECT_ID(N'dbo.AutoContractsWidgetProjection', N'V') IS NOT NULL
DROP VIEW dbo.AutoContractsWidgetProjection;
IF
TYPE_ID(N'dbo.WidgetTvp') IS NOT NULL
DROP TYPE dbo.WidgetTvp;
IF
TYPE_ID(N'dbo.AutoContractsWidgetTvp') IS NOT NULL
DROP TYPE dbo.AutoContractsWidgetTvp;
IF
TYPE_ID(N'contracts.WidgetTvp') IS NOT NULL
DROP TYPE contracts.WidgetTvp;
IF
OBJECT_ID(N'dbo.AutoContractsExecutionProbe', N'U') IS NOT NULL
DROP TABLE dbo.AutoContractsExecutionProbe;
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

CREATE TYPE dbo.AutoContractsWidgetTvp AS TABLE
    (
    ExternalId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL (18, 4) NOT NULL,
    EffectiveDate DATE NULL,
    ChangedAt DATETIMEOFFSET(3) NULL
    );
GO

CREATE TABLE dbo.AutoContractsExecutionProbe
(
    ExecutionCount INT NOT NULL CONSTRAINT DF_AutoContractsExecutionProbe_ExecutionCount DEFAULT 0
);
GO

INSERT INTO dbo.AutoContractsExecutionProbe (ExecutionCount)
VALUES (0);
GO

IF SCHEMA_ID(N'contracts') IS NULL
    EXEC(N'CREATE SCHEMA contracts');
GO

-- Read-only projection used by AutoContracts metadata probes. It exists only to expose
-- stable result-set metadata; no integration test depends on persisted rows here.
CREATE VIEW dbo.AutoContractsWidgetProjection
AS
SELECT Id,
       Name,
       Quantity,
       CreatedAt
FROM dbo.Widgets;
GO

CREATE TYPE contracts.WidgetTvp AS TABLE
    (
    ExternalId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL (18, 4) NOT NULL,
    EffectiveDate DATE NULL,
    ChangedAt DATETIMEOFFSET(3) NULL
    );
GO

CREATE PROCEDURE contracts.usp_SearchWidgets @NamePrefix     NVARCHAR(100) = NULL,
    @MinimumQuantity INT = NULL,
    @CreatedAfter     DATETIME2(3) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.AutoContractsWidgetProjection
WHERE (@NamePrefix IS NULL OR Name LIKE @NamePrefix + N'%')
  AND (@MinimumQuantity IS NULL OR Quantity >= @MinimumQuantity)
  AND (@CreatedAfter IS NULL OR CreatedAt >= @CreatedAfter)
  AND 1 = 0
ORDER BY Id;
END;
GO

CREATE PROCEDURE contracts.usp_PreviewWidgetBatch @Items contracts.WidgetTvp READONLY
AS
BEGIN
    SET
NOCOUNT ON;
SELECT ExternalId,
       Name,
       Quantity,
       UnitPrice,
       EffectiveDate,
       ChangedAt
FROM @Items
WHERE 1 = 0
ORDER BY Name;
END;
GO

CREATE PROCEDURE contracts.usp_QuoteWidget @Name        NVARCHAR(75),
    @Quantity INT,
    @UnitPrice DECIMAL(18, 4),
    @RequestedAt DATETIME2(2) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
UPDATE dbo.AutoContractsExecutionProbe
SET ExecutionCount = ExecutionCount + 1;
SELECT @Name        AS Name,
       @Quantity    AS Quantity,
       @UnitPrice   AS UnitPrice,
       @RequestedAt AS RequestedAt WHERE 1 = 0;
END;
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

-- Five-result-set sproc: list, count, first row, last row, high-quantity rows.
CREATE PROCEDURE dbo.usp_GetWidgetsFiveSets
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
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.Widgets
WHERE Quantity >= 30
ORDER BY Id;
END;
GO

-- AutoContracts read-only metadata fixture: scalar parameters + deterministic DTO shape.
CREATE PROCEDURE dbo.usp_AutoContracts_SearchWidgets @NamePrefix     NVARCHAR(100) = NULL,
    @MinimumQuantity INT = NULL,
    @CreatedAfter     DATETIME2(3) = NULL
AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.AutoContractsWidgetProjection
WHERE (@NamePrefix IS NULL OR Name LIKE @NamePrefix + N'%')
  AND (@MinimumQuantity IS NULL OR Quantity >= @MinimumQuantity)
  AND (@CreatedAfter IS NULL OR CreatedAt >= @CreatedAfter)
  AND 1 = 0
ORDER BY Id;
END;
GO

-- AutoContracts read-only metadata fixture: required scalar input with a nullable switch.
CREATE PROCEDURE dbo.usp_AutoContracts_GetWidgetById @Id              INT,
    @IncludeArchived BIT = 0
AS
BEGIN
    SET
NOCOUNT ON;
SELECT Id, Name, Quantity, CreatedAt
FROM dbo.AutoContractsWidgetProjection
WHERE Id = @Id
  AND @IncludeArchived IN (0, 1)
  AND 1 = 0;
END;
GO

-- AutoContracts read-only metadata fixture: table-valued input and its projected shape.
CREATE PROCEDURE dbo.usp_AutoContracts_PreviewWidgetBatch @Items dbo.AutoContractsWidgetTvp READONLY
AS
BEGIN
    SET
NOCOUNT ON;
SELECT ExternalId,
       Name,
       Quantity,
       UnitPrice,
       EffectiveDate,
       ChangedAt
FROM @Items
WHERE 1 = 0
ORDER BY Name;
END;
GO

-- AutoContracts metadata fixture: scalar facets, output parameter detection, and an
-- execution probe. Metadata discovery must not run this body.
CREATE PROCEDURE dbo.usp_AutoContracts_QuoteWidget @Name        NVARCHAR(75),
    @Quantity INT,
    @UnitPrice DECIMAL(18, 4),
    @RequestedAt DATETIME2(2) = NULL,
    @QuoteTotal DECIMAL(19, 4) OUTPUT
AS
BEGIN
    SET
NOCOUNT ON;
UPDATE dbo.AutoContractsExecutionProbe
SET ExecutionCount = ExecutionCount + 1;
SET
@QuoteTotal = @Quantity * @UnitPrice;
SELECT @Name        AS Name,
       @Quantity    AS Quantity,
       @UnitPrice   AS UnitPrice,
       @RequestedAt AS RequestedAt,
       @QuoteTotal  AS QuoteTotal WHERE 1 = 0;
END;
GO
