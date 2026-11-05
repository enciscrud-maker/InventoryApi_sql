SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.MD_User', N'U') IS NULL
    THROW 51000, 'This upgrade requires the Inventory API user table.', 1;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.TR_PasswordResetRequest', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TR_PasswordResetRequest
    (
        PasswordResetRequestId int IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_TR_PasswordResetRequest PRIMARY KEY,
        RequestId nvarchar(40) NOT NULL,
        UserId int NOT NULL,
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
