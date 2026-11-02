-- HsClassifier schema for MySQL 8 (Pomelo.EntityFrameworkCore.MySql 9.0.0), generated from the EF model with
-- dotnet ef dbcontext script (Database__Provider=MySql). Create-only: no DROP, no ALTER of existing tables.
-- Every text column names utf8mb4 itself, so the database default charset is not touched or required.
-- Run once on an empty database. Running it again fails on the first CREATE TABLE and changes nothing.


CREATE TABLE `AiUsageLogs` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `CreatedAt` datetime(3) NOT NULL,
    `Kind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Provider` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `Host` varchar(100) CHARACTER SET utf8mb4 NULL,
    `Model` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `CaseId` bigint NULL,
    `Status` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `ErrorCode` varchar(30) CHARACTER SET utf8mb4 NULL,
    `HttpStatus` int NULL,
    `LatencyMs` int NOT NULL,
    `PromptTokens` int NULL,
    `CompletionTokens` int NULL,
    `TotalTokens` int NULL,
    CONSTRAINT `PK_AiUsageLogs` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `AppUsers` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Email` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `PasswordHash` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `Role` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_AppUsers` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ClassificationCases` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FactsJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `FinalCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `FinalDescription` longtext CHARACTER SET utf8mb4 NULL,
    `CandidateCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `Confidence` longtext CHARACTER SET utf8mb4 NULL,
    `ConfidenceScore` int NULL,
    `ConfidenceJson` longtext CHARACTER SET utf8mb4 NULL,
    `Status` longtext CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `ClassificationDate` datetime(6) NOT NULL,
    CONSTRAINT `PK_ClassificationCases` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `DailyUsages` (
    `UserId` int NOT NULL,
    `Day` date NOT NULL,
    `Count` int NOT NULL,
    CONSTRAINT `PK_DailyUsages` PRIMARY KEY (`UserId`, `Day`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ExpertCorrections` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `CaseId` bigint NOT NULL,
    `Action` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `BeforeCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `AfterCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `BeforeStatus` varchar(30) CHARACTER SET utf8mb4 NULL,
    `AfterStatus` varchar(30) CHARACTER SET utf8mb4 NULL,
    `Reason` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `ReviewerId` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ExpertCorrections` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ImportBatches` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FileName` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `Checksum` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ReportJson` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TotalNodes` int NOT NULL,
    `ErrorCount` int NOT NULL,
    `WarningCount` int NOT NULL,
    `CreatedBy` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `Kind` varchar(10) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Btki',
    CONSTRAINT `PK_ImportBatches` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `NomenclatureVersions` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `Description` longtext CHARACTER SET utf8mb4 NULL,
    `HsEdition` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `BtkiEdition` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `RegulationNumber` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `EffectiveFrom` datetime(6) NOT NULL,
    `EffectiveTo` datetime(6) NULL,
    `State` longtext CHARACTER SET utf8mb4 NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` longtext CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK_NomenclatureVersions` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ReasoningEvents` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `CaseId` bigint NOT NULL,
    `EventOrder` int NOT NULL,
    `RuleApplied` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Timestamp` datetime(6) NOT NULL,
    `FactsUsedJson` longtext CHARACTER SET utf8mb4 NULL,
    `CandidatesBeforeJson` longtext CHARACTER SET utf8mb4 NULL,
    `CandidatesAfterJson` longtext CHARACTER SET utf8mb4 NULL,
    `Decision` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `Reasoning` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SourceIdsJson` longtext CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `PK_ReasoningEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ReasoningEvents_ClassificationCases_CaseId` FOREIGN KEY (`CaseId`) REFERENCES `ClassificationCases` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `AmbiguityItems` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `BatchId` bigint NOT NULL,
    `Code` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `RowNumber` int NOT NULL,
    `ProposedParent` varchar(15) CHARACTER SET utf8mb4 NULL,
    `Reason` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `Decision` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `CorrectedParent` varchar(15) CHARACTER SET utf8mb4 NULL,
    `Note` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `DecidedBy` varchar(200) CHARACTER SET utf8mb4 NULL,
    `DecidedAt` datetime(6) NULL,
    CONSTRAINT `PK_AmbiguityItems` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_AmbiguityItems_ImportBatches_BatchId` FOREIGN KEY (`BatchId`) REFERENCES `ImportBatches` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `BtkiTariffLines` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `Code` varchar(15) CHARACTER SET utf8mb4 NOT NULL,
    `ParentCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `SectionCode` varchar(10) CHARACTER SET utf8mb4 NULL,
    `ChapterCode` varchar(10) CHARACTER SET utf8mb4 NULL,
    `HeadingCode` varchar(10) CHARACTER SET utf8mb4 NULL,
    `SubheadingCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `IndentLevel` int NOT NULL,
    `DescriptionId` longtext CHARACTER SET utf8mb4 NOT NULL,
    `DescriptionEn` longtext CHARACTER SET utf8mb4 NULL,
    `Unit` varchar(50) CHARACTER SET utf8mb4 NULL,
    `IngestionMethod` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    `EffectiveFrom` datetime(6) NULL,
    `EffectiveTo` datetime(6) NULL,
    CONSTRAINT `PK_BtkiTariffLines` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_BtkiTariffLines_NomenclatureVersions_VersionId` FOREIGN KEY (`VersionId`) REFERENCES `NomenclatureVersions` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `ChapterNotes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `ChapterNumber` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `TargetHeading` varchar(10) CHARACTER SET utf8mb4 NULL,
    `NoteNumber` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ParagraphNumber` longtext CHARACTER SET utf8mb4 NULL,
    `TextId` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TextEn` longtext CHARACTER SET utf8mb4 NULL,
    `EffectType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TargetCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `ReferredCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `NomenclatureVersionId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_ChapterNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ChapterNotes_NomenclatureVersions_NomenclatureVersionId` FOREIGN KEY (`NomenclatureVersionId`) REFERENCES `NomenclatureVersions` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `ExplanatoryNotes` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `HeadingCode` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `SubheadingCode` varchar(15) CHARACTER SET utf8mb4 NULL,
    `ParagraphNumber` longtext CHARACTER SET utf8mb4 NULL,
    `ParagraphType` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `TextContent` longtext CHARACTER SET utf8mb4 NOT NULL,
    `PreviousParagraphId` bigint NULL,
    `NextParagraphId` bigint NULL,
    `ParentSectionId` bigint NULL,
    `ReferredCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `NomenclatureVersionId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_ExplanatoryNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ExplanatoryNotes_NomenclatureVersions_NomenclatureVersionId` FOREIGN KEY (`NomenclatureVersionId`) REFERENCES `NomenclatureVersions` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `SectionNotes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SectionNumber` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `NoteNumber` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `ParagraphNumber` longtext CHARACTER SET utf8mb4 NULL,
    `TextId` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TextEn` longtext CHARACTER SET utf8mb4 NULL,
    `EffectType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TargetCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `ReferredCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `NomenclatureVersionId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_SectionNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SectionNotes_NomenclatureVersions_NomenclatureVersionId` FOREIGN KEY (`NomenclatureVersionId`) REFERENCES `NomenclatureVersions` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `SubheadingNotes` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `HeadingCode` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `NoteNumber` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `TextId` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TextEn` longtext CHARACTER SET utf8mb4 NULL,
    `EffectType` longtext CHARACTER SET utf8mb4 NOT NULL,
    `TargetCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `ReferredCodesJson` longtext CHARACTER SET utf8mb4 NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `NomenclatureVersionId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_SubheadingNotes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_SubheadingNotes_NomenclatureVersions_NomenclatureVersionId` FOREIGN KEY (`NomenclatureVersionId`) REFERENCES `NomenclatureVersions` (`Id`)
) CHARACTER SET=utf8mb4;


CREATE TABLE `FtaRates` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TariffLineId` bigint NOT NULL,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SchemeCode` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `PartnerCountryCode` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `RateValue` decimal(10,4) NOT NULL,
    `RateUnit` longtext CHARACTER SET utf8mb4 NOT NULL,
    `ConditionsSummary` longtext CHARACTER SET utf8mb4 NULL,
    `EffectiveFrom` datetime(6) NULL,
    `EffectiveTo` datetime(6) NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_FtaRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FtaRates_BtkiTariffLines_TariffLineId` FOREIGN KEY (`TariffLineId`) REFERENCES `BtkiTariffLines` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `MfnRates` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TariffLineId` bigint NOT NULL,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `RateValue` decimal(10,4) NOT NULL,
    `RateUnit` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Currency` longtext CHARACTER SET utf8mb4 NULL,
    `QuantityUnit` longtext CHARACTER SET utf8mb4 NULL,
    `Formula` longtext CHARACTER SET utf8mb4 NULL,
    `EffectiveFrom` datetime(6) NULL,
    `EffectiveTo` datetime(6) NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_MfnRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MfnRates_BtkiTariffLines_TariffLineId` FOREIGN KEY (`TariffLineId`) REFERENCES `BtkiTariffLines` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE TABLE `TaxRates` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TariffLineId` bigint NOT NULL,
    `VersionId` char(36) COLLATE ascii_general_ci NOT NULL,
    `TaxType` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `RateValue` decimal(10,4) NOT NULL,
    `RateUnit` longtext CHARACTER SET utf8mb4 NOT NULL,
    `EffectiveFrom` datetime(6) NULL,
    `EffectiveTo` datetime(6) NULL,
    `SourceId` varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsActive` tinyint(1) NOT NULL,
    CONSTRAINT `PK_TaxRates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_TaxRates_BtkiTariffLines_TariffLineId` FOREIGN KEY (`TariffLineId`) REFERENCES `BtkiTariffLines` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;


CREATE INDEX `IX_AiUsageLogs_CreatedAt` ON `AiUsageLogs` (`CreatedAt`);


CREATE INDEX `IX_AiUsageLogs_Status_CreatedAt` ON `AiUsageLogs` (`Status`, `CreatedAt`);


CREATE INDEX `IX_AmbiguityItems_BatchId_Decision` ON `AmbiguityItems` (`BatchId`, `Decision`);


CREATE UNIQUE INDEX `IX_AppUsers_Email` ON `AppUsers` (`Email`);


CREATE INDEX `IX_BtkiTariffLines_ChapterCode` ON `BtkiTariffLines` (`ChapterCode`);


CREATE INDEX `IX_BtkiTariffLines_Code_VersionId` ON `BtkiTariffLines` (`Code`, `VersionId`);


CREATE INDEX `IX_BtkiTariffLines_IsActive_VersionId` ON `BtkiTariffLines` (`IsActive`, `VersionId`);


CREATE INDEX `IX_BtkiTariffLines_ParentCode` ON `BtkiTariffLines` (`ParentCode`);


CREATE INDEX `IX_BtkiTariffLines_VersionId` ON `BtkiTariffLines` (`VersionId`);


CREATE INDEX `IX_ChapterNotes_ChapterNumber_VersionId` ON `ChapterNotes` (`ChapterNumber`, `VersionId`);


CREATE INDEX `IX_ChapterNotes_NomenclatureVersionId` ON `ChapterNotes` (`NomenclatureVersionId`);


CREATE INDEX `IX_ChapterNotes_TargetHeading` ON `ChapterNotes` (`TargetHeading`);


CREATE INDEX `IX_ClassificationCases_FinalCode` ON `ClassificationCases` (`FinalCode`);


CREATE INDEX `IX_ClassificationCases_UserId_CreatedAt` ON `ClassificationCases` (`UserId`, `CreatedAt`);


CREATE INDEX `IX_ExpertCorrections_CaseId` ON `ExpertCorrections` (`CaseId`);


CREATE INDEX `IX_ExplanatoryNotes_HeadingCode_VersionId` ON `ExplanatoryNotes` (`HeadingCode`, `VersionId`);


CREATE INDEX `IX_ExplanatoryNotes_NomenclatureVersionId` ON `ExplanatoryNotes` (`NomenclatureVersionId`);


CREATE INDEX `IX_ExplanatoryNotes_ParagraphType` ON `ExplanatoryNotes` (`ParagraphType`);


CREATE INDEX `IX_FtaRates_TariffLineId_SchemeCode` ON `FtaRates` (`TariffLineId`, `SchemeCode`);


CREATE INDEX `IX_ImportBatches_VersionId` ON `ImportBatches` (`VersionId`);


CREATE INDEX `IX_MfnRates_TariffLineId_VersionId_IsActive` ON `MfnRates` (`TariffLineId`, `VersionId`, `IsActive`);


CREATE INDEX `IX_ReasoningEvents_CaseId_EventOrder` ON `ReasoningEvents` (`CaseId`, `EventOrder`);


CREATE INDEX `IX_SectionNotes_NomenclatureVersionId` ON `SectionNotes` (`NomenclatureVersionId`);


CREATE INDEX `IX_SectionNotes_SectionNumber_VersionId` ON `SectionNotes` (`SectionNumber`, `VersionId`);


CREATE INDEX `IX_SubheadingNotes_HeadingCode_VersionId` ON `SubheadingNotes` (`HeadingCode`, `VersionId`);


CREATE INDEX `IX_SubheadingNotes_NomenclatureVersionId` ON `SubheadingNotes` (`NomenclatureVersionId`);


CREATE INDEX `IX_TaxRates_TariffLineId_TaxType_IsActive` ON `TaxRates` (`TariffLineId`, `TaxType`, `IsActive`);


