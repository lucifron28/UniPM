SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRY
 BEGIN TRANSACTION;
 -- MANIFEST_GUARD
 DECLARE @inspector uniqueidentifier=(SELECT TOP 1 u.Id FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Inspector' ORDER BY u.Id);
 DECLARE @supervisor uniqueidentifier=(SELECT TOP 1 u.Id FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Supervisor' ORDER BY u.Id);
 IF @inspector IS NULL OR @supervisor IS NULL THROW 51000,'Inspector and Supervisor accounts are required.',1;

 DELETE FROM InspectionPhotoEvidence;
 DELETE FROM InspectionWmsReferralAudits;
 DELETE FROM InspectionWmsReferrals;
 DELETE FROM InspectionRecords;
 DELETE FROM InspectionLocationAttempts;
 DELETE FROM PreventiveMaintenanceAcknowledgements;
 DELETE FROM ScheduleAssignmentReassignmentAudits;
 DELETE FROM ScheduleEnrollmentDeferrals;
 DELETE FROM PreventiveMaintenanceForms;
 DELETE FROM PreventiveMaintenanceSchedules;
 DELETE FROM Assets WHERE Id NOT IN (SELECT Id FROM #BaseAssets);
 IF (SELECT COUNT(*) FROM Assets) <> 45 THROW 51001,'Base assets changed; rebuild stopped.',1;
 UPDATE Assets SET Department=CASE WHEN Department='Demo CCMS' THEN 'CCMS' WHEN Department='Demo Library' THEN 'Library' ELSE Department END WHERE Department IN ('Demo CCMS','Demo Library');
 SELECT NEWID() NewId,a.AssetCode+v.Suffix NewCode,a.AssetCategory,a.Department,a.Building,a.Location INTO #NewAssets FROM Assets a CROSS JOIN (VALUES ('-A2'),('-A3')) v(Suffix);
 IF EXISTS(SELECT NewCode FROM #NewAssets GROUP BY NewCode HAVING COUNT(*)>1) OR EXISTS(SELECT 1 FROM #NewAssets n JOIN Assets a ON a.AssetCode=n.NewCode) THROW 51002,'New asset codes conflict.',1;
 INSERT INTO Assets (Id,AssetCode,AssetCategory,DescriptionEmbedding,Building,Department,Location,QrCodeValue,Status,CreatedAt,UpdatedAt,VerificationLatitude,VerificationLongitude,VerificationRadiusMeters)
 SELECT NewId,NewCode,AssetCategory,NULL,Building,Department,Location,CONCAT('UNIPM-',UPPER(AssetCategory),'-',UPPER(LEFT(CONVERT(varchar(36),NewId),8))),'Active',SYSDATETIMEOFFSET(),SYSDATETIMEOFFSET(),NULL,NULL,NULL FROM #NewAssets;
 DECLARE @now datetimeoffset=SYSDATETIMEOFFSET();
 DECLARE @months TABLE (Cat nvarchar(64),M int);
 INSERT INTO @months VALUES ('fire-extinguisher',2),('fire-extinguisher',5),('fire-extinguisher',8),('fire-extinguisher',11),('water-drinking-station',2),('water-drinking-station',5),('water-drinking-station',8),('water-drinking-station',11),('fire-alarm',6),('fire-alarm',12),('emergency-light',6),('emergency-light',12);
 SELECT NEWID() Id,a.Id AssetId,a.AssetCategory,a.Department,a.Building,y.Y Yr,m.M Mo,CONCAT(y.Y,'-',RIGHT(CONCAT('0',m.M),2)) Cycle,ABS(CONVERT(bigint,CHECKSUM(a.Id,y.Y,m.M)))%10 Choice,TODATETIMEOFFSET(CAST(DATEFROMPARTS(y.Y,m.M,1) AS datetime2),'+08:00') MonthStart INTO #Base FROM Assets a JOIN @months m ON m.Cat=a.AssetCategory CROSS JOIN (VALUES (2025),(2026)) y(Y) WHERE a.Department IS NOT NULL;
 SELECT b.*,CASE WHEN MonthStart>@now THEN CASE WHEN Choice<2 THEN NULL WHEN Choice<5 THEN DATEADD(day,-40,@now) ELSE DATEADD(day,-10,@now) END WHEN Choice<2 THEN NULL ELSE DATEADD(day,2,MonthStart) END AssignedAt,
 CASE WHEN MonthStart<@now AND Choice BETWEEN 2 AND 7 AND DATEADD(day,CASE WHEN Choice=2 THEN 40 ELSE 18 END,MonthStart)<@now THEN DATEADD(day,CASE WHEN Choice=2 THEN 40 ELSE 18 END,MonthStart) ELSE NULL END CompletedAt INTO #Rows FROM #Base b;
 SELECT NEWID() Id,Department,AssetCategory,Cycle,Yr,Mo,MIN(MonthStart) MonthStart INTO #Forms FROM #Rows WHERE CompletedAt IS NOT NULL GROUP BY Department,AssetCategory,Cycle,Yr,Mo;
 INSERT INTO PreventiveMaintenanceForms (Id,FileNumber,AssetCategory,Building,Department,PeriodType,Quarter,Semester,Year,AcademicYear,Status,CreatedByUserId,SubmittedByUserId,SubmittedAt,CreatedAt,UpdatedAt,FieldWorkCompletedAt,PmCycle)
 SELECT Id,NULL,AssetCategory,NULL,Department,'Quarter',CONCAT('Q',((Mo-1)/3)+1),NULL,Yr,CONCAT(Yr,'-',Yr+1),'Draft',@inspector,NULL,NULL,MonthStart,MonthStart,NULL,Cycle FROM #Forms;
 INSERT INTO PreventiveMaintenanceSchedules (Id,AssetId,ScheduleDate,PeriodType,Status,Quarter,Semester,Year,AcademicYear,AssignedToUserId,CompletedAt,CreatedAt,UpdatedAt,PmCycle,AssignedSupervisorUserId,FirstInspectorAssignedAt,FirstSupervisorAssignedAt,AssignmentHistoryComplete)
 SELECT Id,AssetId,DATEADD(second,-1,DATEADD(month,1,MonthStart)),'Quarter',CASE WHEN CompletedAt IS NULL THEN 'Due' ELSE 'Completed' END,CONCAT('Q',((Mo-1)/3)+1),NULL,Yr,CONCAT(Yr,'-',Yr+1),CASE WHEN AssignedAt IS NULL THEN NULL ELSE @inspector END,CompletedAt,CASE WHEN AssignedAt IS NOT NULL AND AssignedAt<MonthStart THEN AssignedAt ELSE MonthStart END,COALESCE(CompletedAt,CASE WHEN AssignedAt IS NOT NULL AND AssignedAt<MonthStart THEN AssignedAt ELSE MonthStart END),Cycle,CASE WHEN AssignedAt IS NULL THEN NULL ELSE @supervisor END,AssignedAt,AssignedAt,1 FROM #Rows;
 INSERT INTO InspectionRecords (Id,ScheduleId,AssetId,InspectorUserId,DateInspected,IsOperational,Remarks,ActionsRecommendations,CreatedAt,UpdatedAt,PreventiveMaintenanceFormId,CompletedAt,StartedAt)
 SELECT NEWID(),r.Id,r.AssetId,@inspector,DATEADD(minute,-30,r.CompletedAt),CASE WHEN r.Choice=2 THEN 0 ELSE 1 END,'Synthetic demonstration inspection.',CASE WHEN r.Choice=2 THEN 'Synthetic finding for review.' ELSE NULL END,DATEADD(minute,-30,r.CompletedAt),r.CompletedAt,f.Id,r.CompletedAt,DATEADD(minute,-30,r.CompletedAt) FROM #Rows r JOIN #Forms f ON f.Department=r.Department AND f.AssetCategory=r.AssetCategory AND f.Cycle=r.Cycle WHERE r.CompletedAt IS NOT NULL;
 INSERT INTO ScheduleAssignmentReassignmentAudits (Id,ScheduleId,EventType,AssignmentRole,PreviousAssigneeUserId,NewAssigneeUserId,ChangedByUserId,ChangedAt,Reason)
 SELECT NEWID(),Id,'InitialAssignment','Inspector',NULL,@inspector,@supervisor,AssignedAt,'Synthetic demonstration assignment; rebuilt locally' FROM #Rows WHERE AssignedAt IS NOT NULL;
 INSERT INTO ScheduleAssignmentReassignmentAudits (Id,ScheduleId,EventType,AssignmentRole,PreviousAssigneeUserId,NewAssigneeUserId,ChangedByUserId,ChangedAt,Reason)
 SELECT NEWID(),Id,'InitialAssignment','Supervisor',NULL,@supervisor,@supervisor,AssignedAt,'Synthetic demonstration assignment; rebuilt locally' FROM #Rows WHERE AssignedAt IS NOT NULL;
 SELECT COUNT(*) AS Schedules, SUM(CASE WHEN CompletedAt IS NOT NULL THEN 1 ELSE 0 END) AS Completed, SUM(CASE WHEN AssignedAt IS NULL THEN 1 ELSE 0 END) AS Unassigned FROM #Rows;
 SELECT COUNT(*) AS DraftForms FROM #Forms;
 COMMIT TRANSACTION;
END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK TRANSACTION; THROW; END CATCH;

