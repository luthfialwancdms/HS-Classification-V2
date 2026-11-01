BEGIN TRANSACTION;
CREATE TABLE [DailyUsages] (
    [UserId] int NOT NULL,
    [Day] date NOT NULL,
    [Count] int NOT NULL,
    CONSTRAINT [PK_DailyUsages] PRIMARY KEY ([UserId], [Day])
);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261101160037_AddDailyUsage', N'9.0.19');

COMMIT;
GO

