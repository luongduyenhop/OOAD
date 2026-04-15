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
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 3;
        options.User.RequireUniqueEmail = false;
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
builder.Services.AddControllersWithViews();

var app = builder.Build();

var shouldResetDatabase = false;

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Nếu DB cũ chưa có schema Identity, đánh dấu reset để tránh lỗi runtime "Invalid object name 'AspNetUsers'".
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

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = "admin",
            FullName = "Quản trị viên"
        };

        await userManager.CreateAsync(adminUser, "123");
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
