-- Adds the two tables for the AI tariff estimate (switch in AppSettings, answer per HS code in AiTariffEstimates).
-- Same migration as docs/db/20261103070245_AddAiTariffEstimate.sql for SQL Server. Generated from the EF model with
-- dotnet ef dbcontext script (Database__Provider=MySql). Create-only. Run once after 001_schema.sql.
-- Running it again fails on the first CREATE TABLE and changes nothing.
CREATE TABLE `AiTariffEstimates` (
    `Code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `PpnPercent` decimal(7,4) NULL,
    `PpnBasis` varchar(300) CHARACTER SET utf8mb4 NULL,
    `Pph22Percent` decimal(7,4) NULL,
    `Pph22Basis` varchar(300) CHARACTER SET utf8mb4 NULL,
    `FtaSchemes` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `Model` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(3) NOT NULL,
    CONSTRAINT `PK_AiTariffEstimates` PRIMARY KEY (`Code`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `AppSettings` (
    `Key` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Value` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime(3) NOT NULL,
    `UpdatedBy` varchar(200) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_AppSettings` PRIMARY KEY (`Key`)
) CHARACTER SET=utf8mb4;
