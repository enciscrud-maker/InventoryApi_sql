SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MD_Asset', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MD_Location', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TR_Good', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TR_StockOpname', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TR_StockOpnameDetail', N'U') IS NULL
    THROW 51000, 'This upgrade requires the legacy Inventory API schema.', 1;

DECLARE @needsUpgrade bit = CASE
    WHEN OBJECT_ID(N'dbo.MD_BusinessUnit', N'U') IS NULL
      OR OBJECT_ID(N'dbo.MD_AssetStatus', N'U') IS NULL
      OR COL_LENGTH(N'dbo.MD_Asset', N'StatusId') IS NULL
      OR COL_LENGTH(N'dbo.MD_Asset', N'BusUnitId') IS NULL
      OR COL_LENGTH(N'dbo.MD_Asset', N'ModelId') IS NULL
      OR COL_LENGTH(N'dbo.MD_Location', N'BusUnitId') IS NULL
      OR COL_LENGTH(N'dbo.TR_Good', N'DestinationBusinessUnit') IS NULL
      OR COL_LENGTH(N'dbo.TR_StockOpname', N'BusUnitId') IS NULL
      OR COL_LENGTH(N'dbo.TR_StockOpnameDetail', N'ActualBusUnitId') IS NULL
    THEN 1 ELSE 0 END;

IF @needsUpgrade = 1
   AND (
       EXISTS (SELECT 1 FROM dbo.MD_Asset)
       OR EXISTS (SELECT 1 FROM dbo.MD_Location)
       OR EXISTS (SELECT 1 FROM dbo.TR_Good)
       OR EXISTS (SELECT 1 FROM dbo.TR_StockOpname)
       OR EXISTS (SELECT 1 FROM dbo.TR_StockOpnameDetail)
   )
    THROW 51001, 'Legacy asset, location, goods, or stock-audit rows exist. Back up and migrate those records explicitly before applying this upgrade.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.MD_User', N'IsMainAdmin') IS NULL
    ALTER TABLE dbo.MD_User
        ADD IsMainAdmin bit NOT NULL
            CONSTRAINT DF_MD_User_IsMainAdmin DEFAULT (0);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_User')
      AND name = N'UX_MD_User_IsMainAdmin'
)
    CREATE UNIQUE INDEX UX_MD_User_IsMainAdmin
        ON dbo.MD_User(IsMainAdmin)
        WHERE IsMainAdmin = 1;

IF OBJECT_ID(N'dbo.MD_BusinessUnit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MD_BusinessUnit
    (
        BusUnitId int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_MD_BusinessUnit PRIMARY KEY,
        BusUnitCode nvarchar(30) NOT NULL,
        BusUnitPrefix nvarchar(20) NOT NULL,
        IsActive bit NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.MD_AssetStatus', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MD_AssetStatus
    (
        StatusId int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_MD_AssetStatus PRIMARY KEY,
        StatusCode nvarchar(30) NOT NULL,
        StatusName nvarchar(100) NOT NULL,
        IsActive bit NOT NULL
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_BusinessUnit')
      AND name = N'IX_MD_BusinessUnit_BusUnitCode'
)
    CREATE UNIQUE INDEX IX_MD_BusinessUnit_BusUnitCode
        ON dbo.MD_BusinessUnit(BusUnitCode);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_AssetStatus')
      AND name = N'IX_MD_AssetStatus_StatusCode'
)
    CREATE UNIQUE INDEX IX_MD_AssetStatus_StatusCode
        ON dbo.MD_AssetStatus(StatusCode);

IF NOT EXISTS (
    SELECT 1 FROM dbo.MD_AssetStatus WHERE StatusCode = N'ACTIVE'
)
    INSERT INTO dbo.MD_AssetStatus(StatusCode, StatusName, IsActive)
    VALUES (N'ACTIVE', N'Active', 1);

IF COL_LENGTH(N'dbo.MD_Asset', N'StatusId') IS NULL
    ALTER TABLE dbo.MD_Asset ADD StatusId int NOT NULL;

IF COL_LENGTH(N'dbo.MD_Asset', N'BusUnitId') IS NULL
    ALTER TABLE dbo.MD_Asset ADD BusUnitId int NULL;

IF COL_LENGTH(N'dbo.MD_Asset', N'ModelId') IS NULL
    ALTER TABLE dbo.MD_Asset ADD ModelId int NULL;

IF COL_LENGTH(N'dbo.MD_Location', N'BusUnitId') IS NULL
    ALTER TABLE dbo.MD_Location ADD BusUnitId int NOT NULL;

IF COL_LENGTH(N'dbo.TR_Good', N'DestinationBusinessUnit') IS NULL
    ALTER TABLE dbo.TR_Good ADD DestinationBusinessUnit nvarchar(200) NOT NULL;

IF COL_LENGTH(N'dbo.TR_StockOpname', N'BusUnitId') IS NULL
    ALTER TABLE dbo.TR_StockOpname ADD BusUnitId int NULL;

IF COL_LENGTH(N'dbo.TR_StockOpnameDetail', N'ActualBusUnitId') IS NULL
    ALTER TABLE dbo.TR_StockOpnameDetail ADD ActualBusUnitId int NULL;

IF COL_LENGTH(N'dbo.TR_StockOpnameDetail', N'ActualLocationId') IS NULL
    ALTER TABLE dbo.TR_StockOpnameDetail ADD ActualLocationId int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MD_Asset_MD_AssetStatus_StatusId')
    ALTER TABLE dbo.MD_Asset WITH CHECK
        ADD CONSTRAINT FK_MD_Asset_MD_AssetStatus_StatusId
        FOREIGN KEY (StatusId) REFERENCES dbo.MD_AssetStatus(StatusId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MD_Asset_MD_BusinessUnit_BusUnitId')
    ALTER TABLE dbo.MD_Asset WITH CHECK
        ADD CONSTRAINT FK_MD_Asset_MD_BusinessUnit_BusUnitId
        FOREIGN KEY (BusUnitId) REFERENCES dbo.MD_BusinessUnit(BusUnitId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MD_Asset_MD_Model_ModelId')
    ALTER TABLE dbo.MD_Asset WITH CHECK
        ADD CONSTRAINT FK_MD_Asset_MD_Model_ModelId
        FOREIGN KEY (ModelId) REFERENCES dbo.MD_Model(ModelId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MD_Location_MD_BusinessUnit_BusUnitId')
    ALTER TABLE dbo.MD_Location WITH CHECK
        ADD CONSTRAINT FK_MD_Location_MD_BusinessUnit_BusUnitId
        FOREIGN KEY (BusUnitId) REFERENCES dbo.MD_BusinessUnit(BusUnitId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TR_StockOpname_MD_BusinessUnit_BusUnitId')
    ALTER TABLE dbo.TR_StockOpname WITH CHECK
        ADD CONSTRAINT FK_TR_StockOpname_MD_BusinessUnit_BusUnitId
        FOREIGN KEY (BusUnitId) REFERENCES dbo.MD_BusinessUnit(BusUnitId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TR_StockOpnameDetail_MD_BusinessUnit_ActualBusUnitId')
    ALTER TABLE dbo.TR_StockOpnameDetail WITH CHECK
        ADD CONSTRAINT FK_TR_StockOpnameDetail_MD_BusinessUnit_ActualBusUnitId
        FOREIGN KEY (ActualBusUnitId) REFERENCES dbo.MD_BusinessUnit(BusUnitId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TR_StockOpnameDetail_MD_Location_ActualLocationId')
    ALTER TABLE dbo.TR_StockOpnameDetail WITH CHECK
        ADD CONSTRAINT FK_TR_StockOpnameDetail_MD_Location_ActualLocationId
        FOREIGN KEY (ActualLocationId) REFERENCES dbo.MD_Location(LocationId);

COMMIT TRANSACTION;
