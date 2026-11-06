SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.MD_User', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MD_Asset', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MD_Location', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MD_BusinessUnit', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MD_AssetStatus', N'U') IS NULL
    THROW 51000, 'This upgrade requires the current Inventory API base tables. Apply the legacy schema upgrade first if required.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.MD_Asset', N'AssetCode') IS NULL
    ALTER TABLE dbo.MD_Asset ADD AssetCode nvarchar(100) NULL;
GO

IF COL_LENGTH(N'dbo.MD_Asset', N'AssetStatus') IS NULL
    ALTER TABLE dbo.MD_Asset ADD AssetStatus nvarchar(50) NULL;
GO

UPDATE dbo.MD_Asset
SET AssetCode = AssetPrefix
WHERE AssetCode IS NULL OR LTRIM(RTRIM(AssetCode)) = N'';

IF EXISTS (
    SELECT 1
    FROM dbo.MD_Asset a
    LEFT JOIN dbo.MD_AssetStatus s ON s.StatusId = a.StatusId
    WHERE a.AssetStatus IS NULL AND s.StatusId IS NULL
)
    THROW 51001, 'An asset has no matching asset status; map its StatusId before upgrading.', 1;

UPDATE a
SET AssetStatus = s.StatusName
FROM dbo.MD_Asset a
JOIN dbo.MD_AssetStatus s ON s.StatusId = a.StatusId
WHERE a.AssetStatus IS NULL;

IF EXISTS (SELECT 1 FROM dbo.MD_AssetStatus WHERE LEN(StatusName) > 50)
    THROW 51002, 'An asset status name exceeds the 50-character MD_Asset.AssetStatus limit.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.MD_Asset
    WHERE AssetCode IS NULL OR LTRIM(RTRIM(AssetCode)) = N''
       OR AssetStatus IS NULL
)
    THROW 51003, 'AssetCode or AssetStatus could not be populated.', 1;

IF EXISTS (
    SELECT AssetCode FROM dbo.MD_Asset GROUP BY AssetCode HAVING COUNT_BIG(*) > 1
)
    THROW 51004, 'Existing AssetCode values are not unique; resolve duplicates before upgrading.', 1;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset') AND name = N'AssetCode' AND is_nullable = 1
)
    ALTER TABLE dbo.MD_Asset ALTER COLUMN AssetCode nvarchar(100) NOT NULL;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset') AND name = N'AssetStatus' AND is_nullable = 1
)
    ALTER TABLE dbo.MD_Asset ALTER COLUMN AssetStatus nvarchar(50) NOT NULL;

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset')
      AND name = N'IX_MD_Asset_AssetPrefix'
      AND is_unique = 1
)
    DROP INDEX IX_MD_Asset_AssetPrefix ON dbo.MD_Asset;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset')
      AND name = N'IX_MD_Asset_AssetPrefix'
)
    CREATE INDEX IX_MD_Asset_AssetPrefix ON dbo.MD_Asset(AssetPrefix);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset')
      AND name = N'IX_MD_Asset_AssetCode'
)
    CREATE UNIQUE INDEX IX_MD_Asset_AssetCode ON dbo.MD_Asset(AssetCode);

IF COL_LENGTH(N'dbo.MD_Location', N'LocationCode') IS NULL
    ALTER TABLE dbo.MD_Location ADD LocationCode nvarchar(30) NULL;

IF COL_LENGTH(N'dbo.MD_Location', N'IsActive') IS NULL
    ALTER TABLE dbo.MD_Location ADD IsActive bit NULL;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.MD_Location l
    LEFT JOIN dbo.MD_BusinessUnit b ON b.BusUnitId = l.BusUnitId
    WHERE (l.LocationCode IS NULL OR LTRIM(RTRIM(l.LocationCode)) = N'')
      AND b.BusUnitId IS NULL
)
    THROW 51005, 'A location has no matching business unit; map its BusUnitId before upgrading.', 1;

UPDATE l
SET LocationCode = CONCAT(
        N'LOC-', LEFT(LTRIM(RTRIM(b.BusUnitCode)), 13), N'-',
        RIGHT(REPLICATE('0', 12) + CONVERT(varchar(12), l.LocationId), 12)),
    IsActive = COALESCE(l.IsActive, 1)
FROM dbo.MD_Location l
JOIN dbo.MD_BusinessUnit b ON b.BusUnitId = l.BusUnitId
WHERE l.LocationCode IS NULL OR LTRIM(RTRIM(l.LocationCode)) = N'' OR l.IsActive IS NULL;

IF EXISTS (
    SELECT 1 FROM dbo.MD_Location
    WHERE LocationCode IS NULL OR LTRIM(RTRIM(LocationCode)) = N'' OR IsActive IS NULL
)
    THROW 51006, 'LocationCode or IsActive could not be populated.', 1;

IF EXISTS (
    SELECT LocationCode FROM dbo.MD_Location GROUP BY LocationCode HAVING COUNT_BIG(*) > 1
)
    THROW 51007, 'Existing LocationCode values are not unique; resolve duplicates before upgrading.', 1;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MD_Location') AND name = N'LocationCode' AND is_nullable = 1
)
    ALTER TABLE dbo.MD_Location ALTER COLUMN LocationCode nvarchar(30) NOT NULL;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MD_Location') AND name = N'IsActive' AND is_nullable = 1
)
    ALTER TABLE dbo.MD_Location ALTER COLUMN IsActive bit NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.MD_Location') AND name = N'DF_MD_Location_IsActive'
)
    ALTER TABLE dbo.MD_Location ADD CONSTRAINT DF_MD_Location_IsActive DEFAULT (1) FOR IsActive;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MD_Location') AND name = N'IX_MD_Location_LocationCode'
)
    CREATE UNIQUE INDEX IX_MD_Location_LocationCode ON dbo.MD_Location(LocationCode);

IF OBJECT_ID(N'dbo.TR_PasswordResetRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TR_PasswordResetRequest
    (
        PasswordResetRequestId int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TR_PasswordResetRequest PRIMARY KEY,
        RequestId nvarchar(40) NOT NULL,
        UserId int NULL,
        MemberName nvarchar(200) NOT NULL,
        Nik nvarchar(50) NOT NULL,
        Email nvarchar(200) NOT NULL,
        Phone nvarchar(30) NOT NULL,
        RequestDate datetime2 NOT NULL,
        Status nvarchar(20) NOT NULL,
        ProcessedByUserId int NULL,
        ProcessedAt datetime2 NULL,
        RejectionReason nvarchar(500) NULL,
        CONSTRAINT FK_TR_PasswordResetRequest_MD_User_UserId
            FOREIGN KEY (UserId) REFERENCES dbo.MD_User(UserId),
        CONSTRAINT FK_TR_PasswordResetRequest_MD_User_ProcessedByUserId
            FOREIGN KEY (ProcessedByUserId) REFERENCES dbo.MD_User(UserId),
        CONSTRAINT CK_TR_PasswordResetRequest_Status
            CHECK (Status IN (N'Pending', N'Processed', N'Rejected'))
    );
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.TR_PasswordResetRequest')
      AND name = N'UserId' AND is_nullable = 0
)
    ALTER TABLE dbo.TR_PasswordResetRequest ALTER COLUMN UserId int NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TR_PasswordResetRequest')
      AND name = N'IX_TR_PasswordResetRequest_RequestId'
)
    CREATE UNIQUE INDEX IX_TR_PasswordResetRequest_RequestId
        ON dbo.TR_PasswordResetRequest(RequestId);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TR_PasswordResetRequest')
      AND name = N'IX_TR_PasswordResetRequest_UserId_Status'
)
    CREATE INDEX IX_TR_PasswordResetRequest_UserId_Status
        ON dbo.TR_PasswordResetRequest(UserId, Status);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TR_PasswordResetRequest')
      AND name = N'IX_TR_PasswordResetRequest_Status_RequestDate'
)
    CREATE INDEX IX_TR_PasswordResetRequest_Status_RequestDate
        ON dbo.TR_PasswordResetRequest(Status, RequestDate DESC);

COMMIT TRANSACTION;
