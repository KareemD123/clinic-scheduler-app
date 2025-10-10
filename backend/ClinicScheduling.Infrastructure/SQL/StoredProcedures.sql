-- =============================================
-- Clinic Scheduler - Stored Procedures for Bulk Operations
-- =============================================

-- 1. Bulk Update Appointment Status with Conflict Detection
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[BulkUpdateAppointmentStatus]
    @AppointmentIds NVARCHAR(MAX),
    @NewStatus INT,
    @UpdatedBy UNIQUEIDENTIFIER = NULL,
    @Notes NVARCHAR(1000) = NULL,
    @UpdatedCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @UpdatedAppointments TABLE (Id UNIQUEIDENTIFIER);
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Parse the comma-separated appointment IDs
        WITH AppointmentIdsCTE AS (
            SELECT CAST(value AS UNIQUEIDENTIFIER) AS AppointmentId
            FROM STRING_SPLIT(@AppointmentIds, ',')
            WHERE RTRIM(value) <> ''
        )
        
        -- Update appointments with optimistic concurrency check
        UPDATE a
        SET 
            Status = @NewStatus,
            Notes = COALESCE(@Notes, a.Notes),
            UpdatedAt = GETUTCDATE()
        OUTPUT inserted.Id INTO @UpdatedAppointments
        FROM Appointments a
        INNER JOIN AppointmentIdsCTE ids ON a.Id = ids.AppointmentId
        WHERE a.Status != @NewStatus  -- Only update if status is different
        AND a.AppointmentDate > GETUTCDATE()  -- Only future appointments
        
        SET @UpdatedCount = @@ROWCOUNT;
        
        -- Log the bulk update operation
        INSERT INTO AuditLog (Id, EntityType, EntityId, Action, ChangedBy, ChangedAt, Details)
        SELECT 
            NEWID(),
            'Appointment',
            Id,
            'BulkStatusUpdate',
            @UpdatedBy,
            GETUTCDATE(),
            CONCAT('Status changed to ', @NewStatus, CASE WHEN @Notes IS NOT NULL THEN '. Notes: ' + @Notes ELSE '' END)
        FROM @UpdatedAppointments;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- 2. Bulk Invoice Generation with Business Rules
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[BulkGenerateInvoices]
    @AppointmentIds NVARCHAR(MAX),
    @DefaultAmount DECIMAL(18,2) = 150.00,
    @DueDays INT = 30,
    @CreatedBy UNIQUEIDENTIFIER = NULL,
    @GeneratedCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @GeneratedInvoices TABLE (
        InvoiceId UNIQUEIDENTIFIER,
        AppointmentId UNIQUEIDENTIFIER,
        PatientId UNIQUEIDENTIFIER,
        Amount DECIMAL(18,2)
    );
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        -- Parse appointment IDs and generate invoices
        WITH AppointmentIdsCTE AS (
            SELECT CAST(value AS UNIQUEIDENTIFIER) AS AppointmentId
            FROM STRING_SPLIT(@AppointmentIds, ',')
            WHERE RTRIM(value) <> ''
        ),
        EligibleAppointments AS (
            SELECT 
                a.Id AS AppointmentId,
                a.PatientId,
                a.DoctorId,
                COALESCE(ds.StandardRate, @DefaultAmount) AS Amount
            FROM Appointments a
            INNER JOIN AppointmentIdsCTE ids ON a.Id = ids.AppointmentId
            LEFT JOIN DoctorSpecializations ds ON a.DoctorId = ds.DoctorId
            WHERE a.Status = 2  -- Completed status
            AND NOT EXISTS (
                SELECT 1 FROM Invoices i WHERE i.AppointmentId = a.Id
            )  -- No existing invoice
        )
        
        INSERT INTO Invoices (Id, PatientId, AppointmentId, Amount, Status, DueDate, CreatedAt, UpdatedAt)
        OUTPUT 
            inserted.Id,
            inserted.AppointmentId,
            inserted.PatientId,
            inserted.Amount
        INTO @GeneratedInvoices
        SELECT 
            NEWID(),
            ea.PatientId,
            ea.AppointmentId,
            ea.Amount,
            0,  -- Pending status
            DATEADD(DAY, @DueDays, GETUTCDATE()),
            GETUTCDATE(),
            GETUTCDATE()
        FROM EligibleAppointments ea;
        
        SET @GeneratedCount = @@ROWCOUNT;
        
        -- Update appointment billing status
        UPDATE a
        SET 
            UpdatedAt = GETUTCDATE()
        FROM Appointments a
        INNER JOIN @GeneratedInvoices gi ON a.Id = gi.AppointmentId;
        
        -- Log the bulk invoice generation
        INSERT INTO AuditLog (Id, EntityType, EntityId, Action, ChangedBy, ChangedAt, Details)
        SELECT 
            NEWID(),
            'Invoice',
            InvoiceId,
            'BulkGenerated',
            @CreatedBy,
            GETUTCDATE(),
            CONCAT('Generated for appointment ', AppointmentId, ', Amount: $', Amount)
        FROM @GeneratedInvoices;
        
        COMMIT TRANSACTION;
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
            
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- 3. Complex Patient Billing Summary Report
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetPatientBillingSummary]
    @FromDate DATETIME2,
    @ToDate DATETIME2,
    @PatientId UNIQUEIDENTIFIER = NULL,
    @IncludeZeroBalance BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    
    WITH PatientBillingCTE AS (
        SELECT 
            p.Id AS PatientId,
            p.FirstName + ' ' + p.LastName AS PatientName,
            p.Email,
            p.Phone,
            COUNT(DISTINCT a.Id) AS TotalAppointments,
            COUNT(DISTINCT CASE WHEN a.Status = 2 THEN a.Id END) AS CompletedAppointments,
            COUNT(DISTINCT CASE WHEN a.Status = 3 THEN a.Id END) AS CancelledAppointments,
            COALESCE(SUM(i.Amount), 0) AS TotalBilled,
            COALESCE(SUM(pay.Amount), 0) AS TotalPaid,
            COALESCE(SUM(i.Amount), 0) - COALESCE(SUM(pay.Amount), 0) AS OutstandingBalance,
            COUNT(DISTINCT CASE WHEN i.Status = 0 THEN i.Id END) AS PendingInvoices,
            COUNT(DISTINCT CASE WHEN i.Status = 2 AND i.DueDate < GETUTCDATE() THEN i.Id END) AS OverdueInvoices,
            MAX(a.AppointmentDate) AS LastAppointmentDate,
            MAX(pay.PaymentDate) AS LastPaymentDate
        FROM Patients p
        LEFT JOIN Appointments a ON p.Id = a.PatientId 
            AND a.AppointmentDate BETWEEN @FromDate AND @ToDate
        LEFT JOIN Invoices i ON a.Id = i.AppointmentId
        LEFT JOIN Payments pay ON i.Id = pay.InvoiceId
        WHERE (@PatientId IS NULL OR p.Id = @PatientId)
        GROUP BY p.Id, p.FirstName, p.LastName, p.Email, p.Phone
    )
    
    SELECT 
        PatientId,
        PatientName,
        Email,
        Phone,
        TotalAppointments,
        CompletedAppointments,
        CancelledAppointments,
        TotalBilled,
        TotalPaid,
        OutstandingBalance,
        PendingInvoices,
        OverdueInvoices,
        LastAppointmentDate,
        LastPaymentDate,
        CASE 
            WHEN OutstandingBalance > 0 AND OverdueInvoices > 0 THEN 'Overdue'
            WHEN OutstandingBalance > 0 THEN 'Outstanding'
            WHEN TotalPaid > 0 THEN 'Paid'
            ELSE 'No Activity'
        END AS PaymentStatus
    FROM PatientBillingCTE
    WHERE (@IncludeZeroBalance = 1 OR OutstandingBalance != 0)
    ORDER BY OutstandingBalance DESC, LastAppointmentDate DESC;
END;
GO

-- 4. Doctor Availability Optimization Query
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[FindOptimalAppointmentSlots]
    @DoctorId UNIQUEIDENTIFIER,
    @PreferredDate DATE,
    @DurationMinutes INT = 30,
    @MaxAlternatives INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @WorkingHoursStart TIME = '09:00:00';
    DECLARE @WorkingHoursEnd TIME = '17:00:00';
    DECLARE @SlotDuration INT = @DurationMinutes;
    
    WITH TimeSlots AS (
        -- Generate time slots for the day
        SELECT 
            @PreferredDate AS SlotDate,
            CAST(@WorkingHoursStart AS TIME) AS SlotTime,
            0 AS SlotNumber
        
        UNION ALL
        
        SELECT 
            SlotDate,
            DATEADD(MINUTE, @SlotDuration, SlotTime),
            SlotNumber + 1
        FROM TimeSlots
        WHERE DATEADD(MINUTE, @SlotDuration, SlotTime) <= @WorkingHoursEnd
    ),
    ExistingAppointments AS (
        SELECT 
            CAST(AppointmentDate AS DATE) AS AppointmentDate,
            CAST(AppointmentDate AS TIME) AS AppointmentTime,
            Duration
        FROM Appointments
        WHERE DoctorId = @DoctorId
        AND CAST(AppointmentDate AS DATE) = @PreferredDate
        AND Status IN (0, 1)  -- Scheduled or In Progress
    ),
    AvailableSlots AS (
        SELECT 
            ts.SlotDate,
            ts.SlotTime,
            ts.SlotNumber,
            CASE 
                WHEN EXISTS (
                    SELECT 1 FROM ExistingAppointments ea
                    WHERE ea.AppointmentTime <= ts.SlotTime
                    AND DATEADD(MINUTE, ea.Duration, ea.AppointmentTime) > ts.SlotTime
                ) THEN 0
                ELSE 1
            END AS IsAvailable
        FROM TimeSlots ts
    )
    
    SELECT TOP (@MaxAlternatives)
        DATETIME2FROMPARTS(
            YEAR(SlotDate), MONTH(SlotDate), DAY(SlotDate),
            DATEPART(HOUR, SlotTime), DATEPART(MINUTE, SlotTime), 0, 0, 7
        ) AS AvailableDateTime,
        SlotTime AS AvailableTime,
        @DurationMinutes AS DurationMinutes,
        'Available' AS Status
    FROM AvailableSlots
    WHERE IsAvailable = 1
    ORDER BY SlotTime
    
    OPTION (MAXRECURSION 100);
END;
GO

-- 5. Audit Log Table (for tracking bulk operations)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLog] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [EntityType] NVARCHAR(50) NOT NULL,
        [EntityId] UNIQUEIDENTIFIER NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [ChangedBy] UNIQUEIDENTIFIER NULL,
        [ChangedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Details] NVARCHAR(MAX) NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL
    );
    
    CREATE INDEX IX_AuditLog_EntityType_EntityId ON [dbo].[AuditLog] ([EntityType], [EntityId]);
    CREATE INDEX IX_AuditLog_ChangedAt ON [dbo].[AuditLog] ([ChangedAt]);
END;
GO

-- 6. Performance Optimization Indexes
-- =============================================

-- Composite indexes for common query patterns
CREATE NONCLUSTERED INDEX IX_Appointments_DoctorId_Date_Status 
ON [dbo].[Appointments] ([DoctorId], [AppointmentDate], [Status])
INCLUDE ([PatientId], [Duration], [Notes]);

CREATE NONCLUSTERED INDEX IX_Invoices_PatientId_Status_DueDate 
ON [dbo].[Invoices] ([PatientId], [Status], [DueDate])
INCLUDE ([Amount], [AppointmentId]);

CREATE NONCLUSTERED INDEX IX_Payments_InvoiceId_PaymentDate 
ON [dbo].[Payments] ([InvoiceId], [PaymentDate])
INCLUDE ([Amount], [PaymentMethod]);

-- Filtered indexes for active records
CREATE NONCLUSTERED INDEX IX_Appointments_Active_DoctorId_Date 
ON [dbo].[Appointments] ([DoctorId], [AppointmentDate])
WHERE [Status] IN (0, 1);  -- Scheduled or In Progress only

CREATE NONCLUSTERED INDEX IX_Invoices_Unpaid_DueDate 
ON [dbo].[Invoices] ([DueDate], [PatientId])
WHERE [Status] IN (0, 2);  -- Pending or Overdue only
