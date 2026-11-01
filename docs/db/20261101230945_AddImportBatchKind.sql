BEGIN TRANSACTION;
ALTER TABLE [ImportBatches] ADD [Kind] nvarchar(10) NOT NULL DEFAULT N'Btki';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261101230945_AddImportBatchKind', N'9.0.19');

COMMIT;
GO

