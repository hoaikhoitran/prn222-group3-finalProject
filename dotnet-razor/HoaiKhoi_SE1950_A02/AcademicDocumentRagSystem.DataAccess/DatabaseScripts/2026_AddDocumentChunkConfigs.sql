IF OBJECT_ID(N'dbo.DocumentChunkConfigs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentChunkConfigs
    (
        DocumentChunkConfigId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_DocumentChunkConfigs PRIMARY KEY,
        ChunkMode NVARCHAR(30) NOT NULL,
        ChunkSize INT NOT NULL,
        ChunkOverlap INT NOT NULL,
        MinChunkLength INT NOT NULL,
        MaxPreviewChunks INT NOT NULL,
        IsActive BIT NOT NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_DocumentChunkConfigs_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL
            CONSTRAINT DF_DocumentChunkConfigs_UpdatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedByAccountId INT NULL,
        CONSTRAINT CK_DocumentChunkConfigs_ChunkMode
            CHECK (ChunkMode IN (N'Characters', N'Words', N'Paragraph')),
        CONSTRAINT CK_DocumentChunkConfigs_PositiveSize
            CHECK (ChunkSize > 0),
        CONSTRAINT CK_DocumentChunkConfigs_Overlap
            CHECK (ChunkOverlap >= 0 AND ChunkOverlap < ChunkSize),
        CONSTRAINT CK_DocumentChunkConfigs_MinChunkLength
            CHECK (MinChunkLength >= 0),
        CONSTRAINT CK_DocumentChunkConfigs_MaxPreviewChunks
            CHECK (MaxPreviewChunks BETWEEN 1 AND 1000),
        CONSTRAINT FK_DocumentChunkConfigs_Accounts
            FOREIGN KEY (UpdatedByAccountId) REFERENCES dbo.Accounts(AccountId)
            ON DELETE SET NULL
    );

    CREATE INDEX IX_DocumentChunkConfigs_IsActive
        ON dbo.DocumentChunkConfigs(IsActive);

    CREATE INDEX IX_DocumentChunkConfigs_UpdatedByAccountId
        ON dbo.DocumentChunkConfigs(UpdatedByAccountId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.DocumentChunkConfigs WHERE IsActive = 1)
BEGIN
    INSERT INTO dbo.DocumentChunkConfigs
        (ChunkMode, ChunkSize, ChunkOverlap, MinChunkLength, MaxPreviewChunks, IsActive, Notes)
    VALUES
        (N'Characters', 1500, 250, 80, 200, 1,
         N'Default configuration matching the original preview chunking behavior.');
END;

;WITH RankedActiveConfigs AS
(
    SELECT
        DocumentChunkConfigId,
        ROW_NUMBER() OVER (ORDER BY UpdatedAt DESC, DocumentChunkConfigId DESC) AS RowNumber
    FROM dbo.DocumentChunkConfigs
    WHERE IsActive = 1
)
UPDATE config
SET IsActive = 0
FROM dbo.DocumentChunkConfigs config
INNER JOIN RankedActiveConfigs ranked
    ON ranked.DocumentChunkConfigId = config.DocumentChunkConfigId
WHERE ranked.RowNumber > 1;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_DocumentChunkConfigs_OnlyOneActive'
        AND object_id = OBJECT_ID(N'dbo.DocumentChunkConfigs')
)
BEGIN
    CREATE UNIQUE INDEX UX_DocumentChunkConfigs_OnlyOneActive
        ON dbo.DocumentChunkConfigs(IsActive)
        WHERE IsActive = 1;
END;
