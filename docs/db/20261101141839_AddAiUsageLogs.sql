BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141839_AddAiUsageLogs'
)
BEGIN
    CREATE TABLE [AiUsageLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [CreatedAt] datetime2(3) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Provider] nvarchar(30) NOT NULL,
        [Host] nvarchar(100) NULL,
        [Model] nvarchar(100) NOT NULL,
        [CaseId] bigint NULL,
        [Status] nvarchar(10) NOT NULL,
        [ErrorCode] nvarchar(30) NULL,
        [HttpStatus] int NULL,
        [LatencyMs] int NOT NULL,
        [PromptTokens] int NULL,
        [CompletionTokens] int NULL,
        [TotalTokens] int NULL,
        CONSTRAINT [PK_AiUsageLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141839_AddAiUsageLogs'
)
BEGIN
    CREATE INDEX [IX_AiUsageLogs_CreatedAt] ON [AiUsageLogs] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141839_AddAiUsageLogs'
)
BEGIN
    CREATE INDEX [IX_AiUsageLogs_Status_CreatedAt] ON [AiUsageLogs] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261101141839_AddAiUsageLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261101141839_AddAiUsageLogs', N'9.0.19');
END;

COMMIT;
GO

