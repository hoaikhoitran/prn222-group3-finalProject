/*
    2026_MoveTeacherAssignmentToCourses.sql

    Moves the teacher-course assignment from Accounts.CourseId (wrong model:
    Teacher N - 1 Course) to Courses.TeacherAccountId (correct model:
    Teacher 1 - N Courses; each course has at most one teacher).

    Target database : AcademicRagManagement
    Safe to re-run   : YES (every step is guarded)
    Data loss        : NONE for courses/accounts/documents/chat.
                       Accounts.CourseId is dropped ONLY AFTER its data has
                       been backfilled into Courses.TeacherAccountId.

    Steps:
      1. Add nullable Courses.TeacherAccountId.
      2. Create index IX_Courses_TeacherAccountId (NOT unique — one teacher
         may own many courses).
      3. Create FK_Courses_TeacherAccount -> Accounts(AccountId) ON DELETE SET NULL.
      4. Report legacy conflicts (one course referenced by several teachers
         via Accounts.CourseId). The backfill then picks the teacher with the
         smallest AccountId deterministically — never randomly.
      5. Backfill from Accounts.CourseId (teachers only, Role = 2), without
         overwriting an assignment that already exists.
      6. Drop FK_Accounts_Courses, IX_Accounts_CourseId, and Accounts.CourseId
         so the old column can no longer drift out of sync.
*/

SET NOCOUNT ON;
GO

-- 1. New column ---------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Courses') AND name = N'TeacherAccountId')
BEGIN
    ALTER TABLE dbo.Courses ADD TeacherAccountId INT NULL;
    PRINT 'Added Courses.TeacherAccountId.';
END;
GO

-- 2. Index (not unique: a teacher may own many courses) ------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Courses_TeacherAccountId'
      AND object_id = OBJECT_ID(N'dbo.Courses'))
BEGIN
    CREATE INDEX IX_Courses_TeacherAccountId ON dbo.Courses (TeacherAccountId);
    PRINT 'Created IX_Courses_TeacherAccountId.';
END;
GO

-- 3. Foreign key ---------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Courses_TeacherAccount'
      AND parent_object_id = OBJECT_ID(N'dbo.Courses'))
BEGIN
    ALTER TABLE dbo.Courses
        ADD CONSTRAINT FK_Courses_TeacherAccount
        FOREIGN KEY (TeacherAccountId) REFERENCES dbo.Accounts (AccountId)
        ON DELETE SET NULL;
    PRINT 'Created FK_Courses_TeacherAccount.';
END;
GO

-- 4 + 5. Backfill from the legacy Accounts.CourseId column ---------------
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Accounts') AND name = N'CourseId')
BEGIN
    -- Report every legacy conflict: courses referenced by MORE THAN ONE
    -- teacher. The backfill picks MIN(AccountId) deterministically; the
    -- other teachers lose nothing except this ambiguous legacy claim, and
    -- the admin can reassign explicitly afterwards.
    SELECT
        c.CourseId,
        c.Code,
        a.AccountId  AS ConflictingTeacherId,
        a.Email      AS ConflictingTeacherEmail,
        CASE WHEN a.AccountId = winner.WinnerAccountId THEN 'KEPT (smallest AccountId)' ELSE 'DROPPED' END AS BackfillDecision
    FROM dbo.Courses c
    JOIN dbo.Accounts a
        ON a.CourseId = c.CourseId AND a.Role = 2
    JOIN (
        SELECT CourseId, MIN(AccountId) AS WinnerAccountId
        FROM dbo.Accounts
        WHERE Role = 2 AND CourseId IS NOT NULL
        GROUP BY CourseId
        HAVING COUNT(*) > 1
    ) winner ON winner.CourseId = c.CourseId
    ORDER BY c.CourseId, a.AccountId;

    -- Deterministic backfill: smallest AccountId wins; never overwrite an
    -- assignment that already exists in Courses.TeacherAccountId.
    UPDATE c
    SET c.TeacherAccountId = pick.WinnerAccountId
    FROM dbo.Courses c
    JOIN (
        SELECT CourseId, MIN(AccountId) AS WinnerAccountId
        FROM dbo.Accounts
        WHERE Role = 2 AND CourseId IS NOT NULL
        GROUP BY CourseId
    ) pick ON pick.CourseId = c.CourseId
    WHERE c.TeacherAccountId IS NULL;

    PRINT CONCAT('Backfilled ', @@ROWCOUNT, ' course(s) from Accounts.CourseId.');
END;
GO

-- 6. Drop the legacy column so two sources of truth can never coexist -----
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE name = N'FK_Accounts_Courses'
      AND parent_object_id = OBJECT_ID(N'dbo.Accounts'))
BEGIN
    ALTER TABLE dbo.Accounts DROP CONSTRAINT FK_Accounts_Courses;
    PRINT 'Dropped FK_Accounts_Courses.';
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Accounts_CourseId'
      AND object_id = OBJECT_ID(N'dbo.Accounts'))
BEGIN
    DROP INDEX IX_Accounts_CourseId ON dbo.Accounts;
    PRINT 'Dropped IX_Accounts_CourseId.';
END;
GO

-- CHECK constraints (e.g. CK_Accounts_TeacherCourse) and DEFAULT constraints
-- that reference the legacy column must be dropped before the column can go.
DECLARE @dropSql NVARCHAR(MAX) = N'';

SELECT @dropSql = @dropSql
    + N'ALTER TABLE dbo.Accounts DROP CONSTRAINT ' + QUOTENAME(cc.name) + N';'
FROM sys.check_constraints cc
WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Accounts')
  AND cc.definition LIKE N'%CourseId%';

SELECT @dropSql = @dropSql
    + N'ALTER TABLE dbo.Accounts DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';'
FROM sys.default_constraints dc
JOIN sys.columns c
    ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Accounts')
  AND c.name = N'CourseId';

IF (@dropSql <> N'')
BEGIN
    EXEC sys.sp_executesql @dropSql;
    PRINT 'Dropped constraints depending on Accounts.CourseId.';
END;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Accounts') AND name = N'CourseId')
BEGIN
    ALTER TABLE dbo.Accounts DROP COLUMN CourseId;
    PRINT 'Dropped Accounts.CourseId.';
END;
GO
