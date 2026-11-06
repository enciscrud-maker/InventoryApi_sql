SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MD_Asset', N'U') IS NULL
    THROW 51000, 'This upgrade requires the Inventory API asset table.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.MD_Asset', N'AssetCode') IS NULL
    ALTER TABLE dbo.MD_Asset ADD AssetCode nvarchar(100) NULL;

UPDATE dbo.MD_Asset
SET AssetCode = AssetPrefix
WHERE AssetCode IS NULL OR LTRIM(RTRIM(AssetCode)) = N'';

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MD_Asset')
      AND name = N'AssetCode'
      AND is_nullable = 1
)
    ALTER TABLE dbo.MD_Asset ALTER COLUMN AssetCode nvarchar(100) NOT NULL;

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

COMMIT TRANSACTION;
