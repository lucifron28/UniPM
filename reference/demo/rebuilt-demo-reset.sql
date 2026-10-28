-- Run only inside the wrapper's locked, fingerprint-checked transaction.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
CREATE TABLE #BaseAssets(Id uniqueidentifier PRIMARY KEY);
-- BASE_ASSETS
DECLARE @now datetimeoffset=TODATETIMEOFFSET(DATEADD(hour,10,CAST(@SeedDate AS datetime2)),'+08:00');
DECLARE @year int=YEAR(@SeedDate);
DECLARE @inspector uniqueidentifier=(SELECT TOP 1 u.Id FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Inspector' ORDER BY u.Id);
DECLARE @supervisor uniqueidentifier=(SELECT TOP 1 u.Id FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='Supervisor' ORDER BY u.Id);
DECLARE @gsd uniqueidentifier=(SELECT TOP 1 u.Id FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='GSD' ORDER BY u.Id);
IF @inspector IS NULL OR @supervisor IS NULL OR @gsd IS NULL THROW 51000,'Existing Inspector, Supervisor and GSD accounts are required.',1;
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
-- Synthetic registration must precede the historical cycles we generate.
UPDATE Assets SET CreatedAt=TODATETIMEOFFSET(CAST(DATEFROMPARTS(@year-2,1,1) AS datetime2),'+08:00')
WHERE CreatedAt>TODATETIMEOFFSET(CAST(DATEFROMPARTS(@year-2,1,1) AS datetime2),'+08:00');

-- Preserve existing names; fill sparse department/category groups to six assets.
SELECT Department,MIN(Building) Building,ROW_NUMBER() OVER(ORDER BY Department) DeptNo INTO #Departments
FROM Assets WHERE Department IS NOT NULL GROUP BY Department;
CREATE TABLE #Categories(Cat nvarchar(64),Code varchar(4));
INSERT INTO #Categories VALUES ('fire-extinguisher','FE'),('fire-alarm','FA'),('emergency-light','EL'),('water-drinking-station','WDS');
SELECT d.Department,d.Building,c.Cat,c.Code,n.N,CONCAT(UPPER(REPLACE(d.Department,' ','-')),'-',c.Code,'-',200+n.N) AssetCode
INTO #Additional FROM #Departments d CROSS JOIN #Categories c CROSS JOIN (VALUES(1),(2),(3),(4),(5),(6)) n(N)
WHERE n.N<=6-(SELECT COUNT(*) FROM Assets a WHERE a.Department=d.Department AND a.AssetCategory=c.Cat);
IF EXISTS(SELECT 1 FROM #Additional n JOIN Assets a ON a.AssetCode=n.AssetCode) THROW 51001,'Scenario asset codes conflict.',1;
INSERT INTO Assets(Id,AssetCode,AssetCategory,Building,Department,Location,QrCodeValue,Status,CreatedAt,UpdatedAt)
SELECT NEWID(),AssetCode,Cat,Building,Department,CONCAT('Inspection point ',N),CONCAT('UNIPM-',AssetCode),'Active',DATEADD(year,-2,@now),@now FROM #Additional;
INSERT INTO Assets(Id,AssetCode,AssetCategory,Building,Department,Location,QrCodeValue,Status,CreatedAt,UpdatedAt)
SELECT NEWID(),CONCAT('INV-',c.Code,'-',v.N),c.Cat,d.Building,d.Department,'Inventory storage',CONCAT('UNIPM-INV-',c.Code,'-',v.N),
 CASE WHEN v.N<=2 THEN 'Inactive' ELSE 'Retired' END,DATEADD(year,-2,@now),@now
FROM #Categories c CROSS JOIN (VALUES(1),(2),(3),(4)) v(N) CROSS JOIN (SELECT TOP 1 * FROM #Departments ORDER BY DeptNo) d;
CREATE TABLE #Months(Cat nvarchar(64),Mo int);
INSERT INTO #Months VALUES ('fire-extinguisher',2),('fire-extinguisher',5),('fire-extinguisher',8),('fire-extinguisher',11),('water-drinking-station',2),('water-drinking-station',5),('water-drinking-station',8),('water-drinking-station',11),('fire-alarm',6),('fire-alarm',12),('emergency-light',6),('emergency-light',12);
SELECT d.Department,d.Building,d.DeptNo,c.Cat,y.Yr,m.Mo,CONCAT(y.Yr,'-',RIGHT(CONCAT('0',m.Mo),2)) Cycle,
 TODATETIMEOFFSET(CAST(DATEFROMPARTS(y.Yr,m.Mo,1) AS datetime2),'+08:00') MonthStart,
 ROW_NUMBER() OVER(PARTITION BY c.Cat,y.Yr,m.Mo ORDER BY d.Department) DeptRank,CAST(NULL AS varchar(24)) Scenario
INTO #Batches FROM #Departments d CROSS JOIN #Categories c JOIN #Months m ON m.Cat=c.Cat CROSS JOIN (VALUES(@year-1),(@year)) y(Yr);
UPDATE b SET Scenario=CASE
 WHEN MonthStart>@now THEN CASE WHEN DeptRank<=2 THEN 'Unassigned' WHEN DeptRank<=4 THEN 'SupervisorOnly' ELSE 'AssignedDue' END
 WHEN Yr=@year AND Mo=(SELECT MAX(Mo) FROM #Months m WHERE m.Cat=b.Cat AND m.Mo<=MONTH(@now)) THEN
 CASE WHEN DeptRank<=3 THEN 'Ongoing' WHEN DeptRank<=5 THEN 'Submitted' ELSE 'Acknowledged' END
 WHEN Yr=@year-1 AND Mo=(SELECT MIN(Mo) FROM #Months m WHERE m.Cat=b.Cat) AND DeptRank<=2 THEN 'DraftComplete'
 ELSE 'Acknowledged' END FROM #Batches b;
UPDATE #Batches SET Scenario=CASE WHEN DeptRank<=2 THEN 'Unassigned' WHEN DeptRank<=4 THEN 'SupervisorOnly' ELSE 'AssignedDue' END
WHERE Scenario IN ('Acknowledged','Submitted','DraftComplete') AND DATEADD(day,50,MonthStart)>@now;
SELECT NEWID() Id,a.Id AssetId,a.AssetCode,b.*,ROW_NUMBER() OVER(PARTITION BY b.Department,b.Cat,b.Cycle ORDER BY a.AssetCode) AssetNo,
 CAST(NULL AS datetimeoffset) AssignedAt,CAST(NULL AS datetimeoffset) CompletedAt,CAST(NULL AS datetimeoffset) StartedAt,CAST(NULL AS varchar(16)) Status
INTO #Rows FROM Assets a JOIN #Batches b ON a.Department=b.Department AND a.AssetCategory=b.Cat WHERE a.Status='Active';
UPDATE #Rows SET AssignedAt=CASE WHEN Scenario IN ('Unassigned','SupervisorOnly') THEN NULL
 WHEN Scenario IN ('Ongoing','AssignedDue') THEN DATEADD(day,CASE WHEN AssetNo%2=0 THEN -40 ELSE -10 END,@now)
 ELSE DATEADD(day,CASE WHEN AssetNo%3=0 THEN 25 ELSE 2 END,MonthStart) END;
UPDATE #Rows SET CompletedAt=CASE WHEN Scenario IN ('Acknowledged','Submitted','DraftComplete') THEN
 DATEADD(day,CASE WHEN AssetNo%3=0 THEN 40 WHEN AssetNo%3=1 THEN 18 ELSE 45 END,MonthStart)
 WHEN Scenario='Ongoing' AND AssetNo%6<DeptRank+1 THEN DATEADD(day,-2,@now) ELSE NULL END;
UPDATE #Rows SET Status=CASE WHEN CompletedAt IS NOT NULL THEN 'Completed' WHEN Scenario='Ongoing' THEN 'Ongoing' ELSE 'Due' END,
 StartedAt=CASE WHEN CompletedAt IS NOT NULL THEN DATEADD(minute,-30,CompletedAt) WHEN Scenario='Ongoing' THEN DATEADD(day,-1,@now) ELSE NULL END;
INSERT INTO PreventiveMaintenanceSchedules(Id,AssetId,ScheduleDate,PeriodType,Status,Quarter,Year,AcademicYear,AssignedToUserId,AssignedSupervisorUserId,FirstInspectorAssignedAt,FirstSupervisorAssignedAt,AssignmentHistoryComplete,CompletedAt,CreatedAt,UpdatedAt,PmCycle)
SELECT Id,AssetId,DATEADD(second,-1,DATEADD(month,1,MonthStart)),'Quarter',Status,CONCAT('Q',(Mo-1)/3+1),Yr,CONCAT(Yr,'-',Yr+1),
 CASE WHEN AssignedAt IS NOT NULL THEN @inspector END,CASE WHEN Scenario<>'Unassigned' THEN @supervisor END,AssignedAt,
 CASE WHEN Scenario<>'Unassigned' THEN DATEADD(day,-1,COALESCE(AssignedAt,DATEADD(day,-5,@now))) END,1,CompletedAt,
 CASE WHEN MonthStart<DATEADD(day,-60,@now) THEN MonthStart ELSE DATEADD(day,-60,@now) END,COALESCE(CompletedAt,StartedAt,@now),Cycle FROM #Rows;
SELECT NEWID() Id,Department,Cat,Cycle,Yr,Mo,MIN(MonthStart) MonthStart,Scenario,MAX(CompletedAt) FinishedAt INTO #Forms
FROM #Rows WHERE Scenario IN ('Acknowledged','Submitted','DraftComplete','Ongoing') GROUP BY Department,Cat,Cycle,Yr,Mo,Scenario;
INSERT INTO PreventiveMaintenanceForms(Id,FileNumber,AssetCategory,Building,Department,PeriodType,Quarter,Year,AcademicYear,Status,CreatedByUserId,SubmittedByUserId,SubmittedAt,CreatedAt,UpdatedAt,FieldWorkCompletedAt,PmCycle)
SELECT Id,CASE WHEN Scenario IN ('Acknowledged','Submitted') THEN CONCAT('PM-',Yr,'-SYN-',LEFT(REPLACE(CONVERT(varchar(36),Id),'-',''),16)) END,Cat,NULL,Department,'Quarter',CONCAT('Q',(Mo-1)/3+1),Yr,CONCAT(Yr,'-',Yr+1),
 CASE WHEN Scenario='Acknowledged' THEN 'Acknowledged' WHEN Scenario='Submitted' THEN 'Submitted' ELSE 'Draft' END,@inspector,
 CASE WHEN Scenario IN ('Acknowledged','Submitted') THEN @inspector END,CASE WHEN Scenario IN ('Acknowledged','Submitted') THEN DATEADD(hour,1,FinishedAt) END,
 MonthStart,COALESCE(DATEADD(day,1,FinishedAt),@now),CASE WHEN Scenario<>'Ongoing' THEN FinishedAt END,Cycle FROM #Forms;
INSERT INTO InspectionRecords(Id,ScheduleId,AssetId,InspectorUserId,DateInspected,StartedAt,CompletedAt,DateAccomplished,IsOperational,Remarks,ActionsRecommendations,CreatedAt,UpdatedAt,PreventiveMaintenanceFormId,WaterCheckUvLight,WaterReplaceCarbonFilter,WaterReplaceSedimentFilter)
SELECT NEWID(),r.Id,AssetId,@inspector,StartedAt,StartedAt,CompletedAt,CompletedAt,
 CASE WHEN CompletedAt IS NOT NULL AND AssetNo%3=0 THEN 0 ELSE 1 END,CONCAT('Synthetic scenario v2: ',r.Scenario,'. Asset ',AssetCode,'.'),
 CASE WHEN CompletedAt IS NOT NULL AND AssetNo%3=0 THEN 'Synthetic finding: arrange corrective assessment.' END,StartedAt,COALESCE(CompletedAt,StartedAt),f.Id,
 CASE WHEN r.Cat='water-drinking-station' AND CompletedAt IS NOT NULL THEN 1 END,
 CASE WHEN r.Cat='water-drinking-station' AND CompletedAt IS NOT NULL THEN 1 END,
 CASE WHEN r.Cat='water-drinking-station' AND CompletedAt IS NOT NULL THEN 1 END
FROM #Rows r JOIN #Forms f ON f.Department=r.Department AND f.Cat=r.Cat AND f.Cycle=r.Cycle WHERE r.CompletedAt IS NOT NULL;
DECLARE @signature varchar(max)='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2lD8AAAAASUVORK5CYII=';
DECLARE @signatureBytes varbinary(max)=CAST(N'' AS xml).value('xs:base64Binary(sql:variable("@signature"))','varbinary(max)');
INSERT INTO PreventiveMaintenanceAcknowledgements(Id,FormId,SignatoryName,SignatoryPosition,SignatureData,SignatureContentType,SignatureChecksum,CapturedByUserId,AcknowledgedAt)
SELECT NEWID(),Id,'Synthetic department representative','Synthetic acknowledgement for demonstration',@signature,'image/png',CONVERT(varchar(64),HASHBYTES('SHA2_256',@signatureBytes),2),@inspector,DATEADD(day,1,FinishedAt) FROM #Forms WHERE Scenario='Acknowledged';
INSERT INTO ScheduleAssignmentReassignmentAudits(Id,ScheduleId,EventType,AssignmentRole,PreviousAssigneeUserId,NewAssigneeUserId,ChangedByUserId,ChangedAt,Reason)
SELECT NEWID(),s.Id,'InitialAssignment','Supervisor',NULL,@supervisor,@gsd,s.FirstSupervisorAssignedAt,'Synthetic scenario v2: initial assignment' FROM PreventiveMaintenanceSchedules s WHERE s.FirstSupervisorAssignedAt IS NOT NULL
UNION ALL SELECT NEWID(),s.Id,'InitialAssignment','Inspector',NULL,@inspector,@supervisor,s.FirstInspectorAssignedAt,'Synthetic scenario v2: initial assignment' FROM PreventiveMaintenanceSchedules s WHERE s.FirstInspectorAssignedAt IS NOT NULL;
INSERT INTO ScheduleAssignmentReassignmentAudits(Id,ScheduleId,EventType,AssignmentRole,PreviousAssigneeUserId,NewAssigneeUserId,ChangedByUserId,ChangedAt,Reason)
SELECT NEWID(),Id,'Unassignment','Inspector',@inspector,NULL,@supervisor,DATEADD(day,1,AssignedAt),'Synthetic scenario v2: temporary unassignment' FROM #Rows WHERE Scenario='Ongoing' AND AssetNo=1
UNION ALL SELECT NEWID(),Id,'Reassignment','Inspector',NULL,@inspector,@supervisor,DATEADD(day,2,AssignedAt),'Synthetic scenario v2: original period retained' FROM #Rows WHERE Scenario='Ongoing' AND AssetNo=1;
INSERT INTO InspectionWmsReferrals(InspectionId,ExternalPmNumber,Revision,RecordedByUserId,RecordedAt,LastUpdatedByUserId,LastUpdatedAt)
SELECT i.Id,CONCAT('SYN-WMS-',REPLACE(r.AssetCode,' ','-'),'-',r.Cycle),1,@gsd,DATEADD(day,2,f.FinishedAt),@gsd,DATEADD(day,2,f.FinishedAt)
FROM InspectionRecords i JOIN #Rows r ON r.Id=i.ScheduleId JOIN #Forms f ON f.Id=i.PreventiveMaintenanceFormId
WHERE r.Scenario='Acknowledged' AND i.IsOperational=0 AND r.AssetNo%2=0;
INSERT INTO InspectionWmsReferralAudits(Id,InspectionId,PreviousExternalPmNumber,NewExternalPmNumber,Revision,ChangedByUserId,ChangedAt)
SELECT NEWID(),InspectionId,NULL,ExternalPmNumber,1,RecordedByUserId,RecordedAt FROM InspectionWmsReferrals;
SELECT TOP 1 i.Id InspectionId,i.AssetId,i.ScheduleId,i.InspectorUserId,i.StartedAt INTO #Evidence FROM InspectionRecords i JOIN #Rows r ON r.Id=i.ScheduleId WHERE r.Scenario='DraftComplete' ORDER BY r.Cat,r.Department,r.AssetCode;
INSERT INTO InspectionLocationAttempts(Id,AssetId,ScheduleId,ActorUserId,CapturedAt,MeasuredLatitude,MeasuredLongitude,AccuracyMeters,ExpectedLatitude,ExpectedLongitude,ExpectedRadiusMeters,DistanceMeters,Outcome,AccuracyMode,AcquisitionDurationMs,DevicePositionTimestamp,HasAccuracy,IsMocked)
SELECT NEWID(),AssetId,ScheduleId,InspectorUserId,StartedAt,0,0,5,NULL,NULL,NULL,NULL,'NotConfigured','Precise',1000,StartedAt,1,1 FROM #Evidence;
INSERT INTO InspectionPhotoEvidence(InspectionId,StorageKey,LengthBytes,UploadedAt,Revision)
SELECT InspectionId,'a46cc8d993104499827b000000000002.jpg',-- PHOTO_LENGTH
 StartedAt,1 FROM #Evidence;

IF EXISTS(SELECT 1 FROM #Rows WHERE CompletedAt>@now OR (CompletedAt IS NOT NULL AND CompletedAt<AssignedAt)) THROW 51010,'Invalid completion chronology.',1;
IF EXISTS(SELECT 1 FROM InspectionRecords i JOIN PreventiveMaintenanceSchedules s ON s.Id=i.ScheduleId WHERE i.CompletedAt IS NULL OR s.Status<>'Completed' OR i.CompletedAt<>s.CompletedAt) THROW 51015,'Saved inspection and schedule disagree.',1;
IF EXISTS(SELECT 1 FROM #Forms f JOIN #Rows r ON r.Department=f.Department AND r.Cat=f.Cat AND r.Cycle=f.Cycle WHERE f.Scenario IN ('Acknowledged','Submitted') AND r.CompletedAt IS NULL) THROW 51011,'Incomplete locked form.',1;
IF (SELECT COUNT(DISTINCT AssetCategory) FROM PreventiveMaintenanceSchedules s JOIN Assets a ON a.Id=s.AssetId WHERE s.Status='Ongoing')<>4 THROW 51012,'Ongoing coverage missing.',1;
IF EXISTS(SELECT Cat FROM #Categories EXCEPT SELECT AssetCategory FROM PreventiveMaintenanceForms WHERE Status='Submitted') THROW 51013,'Submitted coverage missing.',1;
IF EXISTS(SELECT a.AssetCategory FROM InspectionRecords i JOIN Assets a ON a.Id=i.AssetId JOIN PreventiveMaintenanceForms f ON f.Id=i.PreventiveMaintenanceFormId WHERE f.Status='Acknowledged' GROUP BY a.AssetCategory HAVING COUNT(*)<30) THROW 51014,'Official history too sparse.',1;
