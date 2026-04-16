-- SchedulingApp - Full SQL Server schema (Identity + Business tables)
-- Target: SQL Server
-- Note: Account admin is seeded by application (UserManager) at runtime.

SET NOCOUNT ON;
GO

IF DB_ID(N'SchedulingApp') IS NULL
BEGIN
    CREATE DATABASE [SchedulingApp];
END
GO

USE [SchedulingApp];
GO

/* =========================
   Identity tables
   ========================= */

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetRoleClaims];
IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserClaims];
IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserLogins];
IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserRoles];
IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUserTokens];
IF OBJECT_ID(N'[dbo].[ReminderNotifications]', N'U') IS NOT NULL DROP TABLE [dbo].[ReminderNotifications];
IF OBJECT_ID(N'[dbo].[Tasks]', N'U') IS NOT NULL DROP TABLE [dbo].[Tasks];
IF OBJECT_ID(N'[dbo].[Categories]', N'U') IS NOT NULL DROP TABLE [dbo].[Categories];
IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetRoles];
IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL DROP TABLE [dbo].[AspNetUsers];
GO

CREATE TABLE [dbo].[AspNetUsers]
(
    [Id]                   INT IDENTITY(1,1) NOT NULL,
    [UserName]             NVARCHAR(256) NULL,
    [NormalizedUserName]   NVARCHAR(256) NULL,
    [Email]                NVARCHAR(256) NULL,
    [NormalizedEmail]      NVARCHAR(256) NULL,
    [EmailConfirmed]       BIT NOT NULL CONSTRAINT [DF_AspNetUsers_EmailConfirmed] DEFAULT(0),
    [PasswordHash]         NVARCHAR(MAX) NULL,
    [SecurityStamp]        NVARCHAR(MAX) NULL,
    [ConcurrencyStamp]     NVARCHAR(MAX) NULL,
    [PhoneNumber]          NVARCHAR(MAX) NULL,
    [PhoneNumberConfirmed] BIT NOT NULL CONSTRAINT [DF_AspNetUsers_PhoneNumberConfirmed] DEFAULT(0),
    [TwoFactorEnabled]     BIT NOT NULL CONSTRAINT [DF_AspNetUsers_TwoFactorEnabled] DEFAULT(0),
    [LockoutEnd]           DATETIMEOFFSET(7) NULL,
    [LockoutEnabled]       BIT NOT NULL CONSTRAINT [DF_AspNetUsers_LockoutEnabled] DEFAULT(0),
    [AccessFailedCount]    INT NOT NULL CONSTRAINT [DF_AspNetUsers_AccessFailedCount] DEFAULT(0),
    [FullName]             NVARCHAR(200) NOT NULL CONSTRAINT [DF_AspNetUsers_FullName] DEFAULT(N''),
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[AspNetRoles]
(
    [Id]               INT IDENTITY(1,1) NOT NULL,
    [Name]             NVARCHAR(256) NULL,
    [NormalizedName]   NVARCHAR(256) NULL,
    [ConcurrencyStamp] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[AspNetUserClaims]
(
    [Id]         INT IDENTITY(1,1) NOT NULL,
    [UserId]     INT NOT NULL,
    [ClaimType]  NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[AspNetUserLogins]
(
    [LoginProvider]       NVARCHAR(450) NOT NULL,
    [ProviderKey]         NVARCHAR(450) NOT NULL,
    [ProviderDisplayName] NVARCHAR(MAX) NULL,
    [UserId]              INT NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[AspNetUserRoles]
(
    [UserId] INT NOT NULL,
    [RoleId] INT NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[AspNetUserTokens]
(
    [UserId]        INT NOT NULL,
    [LoginProvider] NVARCHAR(450) NOT NULL,
    [Name]          NVARCHAR(450) NOT NULL,
    [Value]         NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[AspNetRoleClaims]
(
    [Id]         INT IDENTITY(1,1) NOT NULL,
    [RoleId]     INT NOT NULL,
    [ClaimType]  NVARCHAR(MAX) NULL,
    [ClaimValue] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims]([RoleId]);
CREATE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles]([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims]([UserId]);
CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins]([UserId]);
CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles]([RoleId]);
CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers]([NormalizedEmail]);
CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers]([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

/* =========================
   Business tables
   ========================= */

CREATE TABLE [dbo].[Categories]
(
    [Id]   INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [dbo].[Tasks]
(
    [Id]           INT IDENTITY(1,1) NOT NULL,
    [Title]        NVARCHAR(200) NOT NULL,
    [DateTime]     DATETIME2 NOT NULL,
    [CategoryId]   INT NULL,
    [UserId]       INT NOT NULL,
    [Status]       INT NOT NULL CONSTRAINT [DF_Tasks_Status] DEFAULT(0),
    [Priority]     INT NOT NULL CONSTRAINT [DF_Tasks_Priority] DEFAULT(1),
    [ReminderTime] DATETIME2 NULL,
    [TaskType]     NVARCHAR(20) NOT NULL,
    [Frequency]    NVARCHAR(50) NULL,
    [ExcludedDates] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tasks_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [FK_Tasks_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_Tasks_CategoryId] ON [dbo].[Tasks]([CategoryId]);
CREATE INDEX [IX_Tasks_UserId] ON [dbo].[Tasks]([UserId]);
CREATE INDEX [IX_Tasks_UserId_DateTime] ON [dbo].[Tasks]([UserId], [DateTime]);
CREATE INDEX [IX_Tasks_UserId_Status] ON [dbo].[Tasks]([UserId], [Status]);
CREATE INDEX [IX_Tasks_UserId_CategoryId] ON [dbo].[Tasks]([UserId], [CategoryId]);
CREATE INDEX [IX_Tasks_UserId_Priority] ON [dbo].[Tasks]([UserId], [Priority]);
GO

CREATE TABLE [dbo].[ReminderNotifications]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] INT NOT NULL,
    [TaskId] INT NOT NULL,
    [ReminderTime] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ReminderNotifications_CreatedAt] DEFAULT (SYSUTCDATETIME()),
    [SentAt] DATETIME2 NULL,
    [IsRead] BIT NOT NULL CONSTRAINT [DF_ReminderNotifications_IsRead] DEFAULT (0),
    [Message] NVARCHAR(500) NOT NULL,
    CONSTRAINT [PK_ReminderNotifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ReminderNotifications_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReminderNotifications_Tasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Tasks]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ReminderNotifications_UserId_IsRead] ON [dbo].[ReminderNotifications]([UserId], [IsRead]);
CREATE UNIQUE INDEX [IX_ReminderNotifications_TaskId_ReminderTime] ON [dbo].[ReminderNotifications]([TaskId], [ReminderTime]);
GO

/* =========================
   Seed sample categories
   ========================= */

INSERT INTO [dbo].[Categories] ([Name])
VALUES (N'Công việc'), (N'Cá nhân'), (N'Học tập');
GO

-- Optional sample tasks for admin if account exists.
DECLARE @AdminId INT = (
    SELECT TOP 1 [Id]
    FROM [dbo].[AspNetUsers]
    WHERE [NormalizedUserName] = N'ADMIN'
);

IF @AdminId IS NOT NULL
BEGIN
    INSERT INTO [dbo].[Tasks] ([Title], [DateTime], [CategoryId], [UserId], [Status], [Priority], [ReminderTime], [TaskType], [Frequency], [ExcludedDates])
    VALUES
    (N'Họp khởi động dự án', SYSDATETIME(), 1, @AdminId, 0, 2, NULL, N'Simple', NULL, NULL),
    (N'Đọc sách mỗi ngày', SYSDATETIME(), 2, @AdminId, 0, 1, NULL, N'Recurring', N'Daily', NULL);
END
GO
