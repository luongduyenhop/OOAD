using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchedulingApp.Data;
using SchedulingApp.Models;
using SchedulingApp.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddHostedService<ReminderBackgroundService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

var shouldResetDatabase = false;

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (context.Database.CanConnect() && !TableExists(context, "AspNetUsers"))
    {
        shouldResetDatabase = true;
    }

    if (shouldResetDatabase)
    {
        context.Database.EnsureDeleted();
    }
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    EnsureTaskPriorityColumn(context);
    EnsureReminderNotificationsTable(context);
    EnsureCategoriesPerUser(context);

    if (app.Environment.IsDevelopment())
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = "admin@scheduler.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Quan tri vien"
            };

            await userManager.CreateAsync(adminUser, "Admin1234");
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tasks}/{action=Index}/{id?}");

app.Run();

static bool TableExists(AppDbContext context, string tableName)
{
    var conn = context.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
    {
        conn.Open();
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT 1
        FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_NAME = @tableName";

    var p = cmd.CreateParameter();
    p.ParameterName = "@tableName";
    p.Value = tableName;
    cmd.Parameters.Add(p);

    return cmd.ExecuteScalar() != null;
}

static bool ColumnExists(AppDbContext context, string tableName, string columnName)
{
    var conn = context.Database.GetDbConnection();
    if (conn.State != System.Data.ConnectionState.Open)
    {
        conn.Open();
    }

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";

    var tableParam = cmd.CreateParameter();
    tableParam.ParameterName = "@tableName";
    tableParam.Value = tableName;
    cmd.Parameters.Add(tableParam);

    var columnParam = cmd.CreateParameter();
    columnParam.ParameterName = "@columnName";
    columnParam.Value = columnName;
    cmd.Parameters.Add(columnParam);

    return cmd.ExecuteScalar() != null;
}

static void EnsureTaskPriorityColumn(AppDbContext context)
{
    if (!TableExists(context, "Tasks") || ColumnExists(context, "Tasks", "Priority"))
    {
        return;
    }

    context.Database.ExecuteSqlRaw(
        "ALTER TABLE [dbo].[Tasks] ADD [Priority] INT NOT NULL CONSTRAINT [DF_Tasks_Priority] DEFAULT(1);");
}

static void EnsureReminderNotificationsTable(AppDbContext context)
{
    if (TableExists(context, "ReminderNotifications"))
    {
        return;
    }

    context.Database.ExecuteSqlRaw(@"
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
CREATE INDEX [IX_ReminderNotifications_UserId_IsRead] ON [dbo].[ReminderNotifications]([UserId], [IsRead]);
CREATE UNIQUE INDEX [IX_ReminderNotifications_TaskId_ReminderTime] ON [dbo].[ReminderNotifications]([TaskId], [ReminderTime]);
");
}

static void EnsureCategoriesPerUser(AppDbContext context)
{
    if (!TableExists(context, "Categories"))
    {
        return;
    }

    // Fresh DB created from current model already has UserId.
    if (ColumnExists(context, "Categories", "UserId"))
    {
        return;
    }

    // Migrate legacy shared Categories -> per-user Categories based on Tasks ownership.
    context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[Categories_New]', N'U') IS NOT NULL
    DROP TABLE [dbo].[Categories_New];

CREATE TABLE [dbo].[Categories_New]
(
    [Id] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(50) NOT NULL,
    [UserId] INT NOT NULL,
    CONSTRAINT [PK_Categories_New] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_New_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_Categories_New_UserId_Name] ON [dbo].[Categories_New]([UserId], [Name]);

INSERT INTO [dbo].[Categories_New] ([Name], [UserId])
SELECT DISTINCT c.[Name], t.[UserId]
FROM [dbo].[Tasks] t
INNER JOIN [dbo].[Categories] c ON c.[Id] = t.[CategoryId]
WHERE t.[CategoryId] IS NOT NULL;

DECLARE @fk sysname;
SELECT @fk = fk.name
FROM sys.foreign_keys fk
WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[Tasks]')
  AND fk.referenced_object_id = OBJECT_ID(N'[dbo].[Categories]');
IF @fk IS NOT NULL EXEC('ALTER TABLE [dbo].[Tasks] DROP CONSTRAINT [' + @fk + ']');

UPDATE t
SET t.[CategoryId] = cn.[Id]
FROM [dbo].[Tasks] t
INNER JOIN [dbo].[Categories] c ON c.[Id] = t.[CategoryId]
INNER JOIN [dbo].[Categories_New] cn ON cn.[UserId] = t.[UserId] AND cn.[Name] = c.[Name];

DROP TABLE [dbo].[Categories];
EXEC sp_rename 'dbo.Categories_New', 'Categories';

ALTER TABLE [dbo].[Tasks] WITH CHECK ADD CONSTRAINT [FK_Tasks_Categories_CategoryId]
FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]);

CREATE INDEX [IX_Categories_UserId] ON [dbo].[Categories]([UserId]);
CREATE UNIQUE INDEX [IX_Categories_UserId_Name] ON [dbo].[Categories]([UserId], [Name]);
");
}
