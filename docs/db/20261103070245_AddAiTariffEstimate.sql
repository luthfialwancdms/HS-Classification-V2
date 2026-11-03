BEGIN TRANSACTION;
CREATE TABLE [AiTariffEstimates] (
    [Code] nvarchar(20) NOT NULL,
    [PpnPercent] decimal(7,4) NULL,
    [PpnBasis] nvarchar(300) NULL,
    [Pph22Percent] decimal(7,4) NULL,
    [Pph22Basis] nvarchar(300) NULL,
    [FtaSchemes] nvarchar(1000) NOT NULL,
    [Model] nvarchar(100) NOT NULL,
    [CreatedAt] datetime2(3) NOT NULL,
    CONSTRAINT [PK_AiTariffEstimates] PRIMARY KEY ([Code])
);

CREATE TABLE [AppSettings] (
    [Key] nvarchar(100) NOT NULL,
    [Value] nvarchar(200) NOT NULL,
    [UpdatedAt] datetime2(3) NOT NULL,
    [UpdatedBy] nvarchar(200) NULL,
    CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Key])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261103070245_AddAiTariffEstimate', N'9.0.19');

COMMIT;
GO

