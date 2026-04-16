using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SchedulingApp.Data;
using SchedulingApp.Models;

namespace SchedulingApp.Services
{
    public interface IReminderService
    {
        Task<int> ProcessDueRemindersAsync(CancellationToken ct = default);
        Task<List<ReminderNotification>> GetUnreadNotificationsAsync(int userId, int take = 10);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
        Task<(bool Success, string? Error)> SendTestEmailAsync(int userId, CancellationToken ct = default);
    }

    public class ReminderService : IReminderService
    {
        private readonly AppDbContext _context;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(AppDbContext context, IEmailSender emailSender, ILogger<ReminderService> logger)
        {
            _context = context;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
        {
            var now = DateTime.Now;

            var dueTasks = await _context.Tasks
                .Where(t => t.ReminderTime.HasValue
                            && t.ReminderTime.Value <= now
                            && t.Status != AppTaskStatus.Completed
                            && t.Status != AppTaskStatus.Archived)
                .Select(t => new
                {
                    t.Id,
                    t.UserId,
                    t.Title,
                    ReminderTime = t.ReminderTime!.Value
                })
                .ToListAsync(ct);

            var created = 0;
            foreach (var task in dueTasks)
            {
                var exists = await _context.ReminderNotifications
                    .AnyAsync(r => r.TaskId == task.Id && r.ReminderTime == task.ReminderTime, ct);
                if (exists)
                {
                    continue;
                }

                _context.ReminderNotifications.Add(new ReminderNotification
                {
                    UserId = task.UserId,
                    TaskId = task.Id,
                    ReminderTime = task.ReminderTime,
                    Message = $"Nhắc việc: \"{task.Title}\" đến hạn lúc {task.ReminderTime:HH:mm dd/MM/yyyy}."
                });
                created++;
            }

            if (created > 0)
            {
                await _context.SaveChangesAsync(ct);
            }

            await TrySendPendingReminderEmailsAsync(ct);
            return created;
        }

        private async Task TrySendPendingReminderEmailsAsync(CancellationToken ct)
        {
            if (!_emailSender.IsConfigured)
            {
                _logger.LogInformation("SMTP not configured; skip sending reminder emails.");
                return;
            }

            var pending = await _context.ReminderNotifications
                .Where(r => r.SentAt == null)
                .Join(
                    _context.Users,
                    r => r.UserId,
                    u => u.Id,
                    (r, u) => new { Reminder = r, User = u })
                .Join(
                    _context.Tasks,
                    ru => ru.Reminder.TaskId,
                    t => t.Id,
                    (ru, t) => new { ru.Reminder, ru.User, Task = t })
                .OrderBy(x => x.Reminder.ReminderTime)
                .Take(50)
                .ToListAsync(ct);

            if (pending.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Found {Count} pending reminder emails.", pending.Count);

            foreach (var item in pending)
            {
                if (string.IsNullOrWhiteSpace(item.User.Email))
                {
                    item.Reminder.SentAt = DateTime.Now;
                    continue;
                }

                try
                {
                    var sent = await _emailSender.SendReminderEmailAsync(
                        item.User.Email,
                        item.Task.Title,
                        item.Reminder.Message);
                    if (sent)
                    {
                        item.Reminder.SentAt = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    // Keep SentAt = null so background service can retry in next cycles.
                    _logger.LogError(ex, "Failed to send reminder email. UserId={UserId} TaskId={TaskId}", item.User.Id, item.Task.Id);
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<List<ReminderNotification>> GetUnreadNotificationsAsync(int userId, int take = 10)
        {
            return await _context.ReminderNotifications
                .AsNoTracking()
                .Where(r => r.UserId == userId && !r.IsRead)
                .OrderByDescending(r => r.ReminderTime)
                .Take(take)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _context.ReminderNotifications
                .FirstOrDefaultAsync(r => r.Id == notificationId && r.UserId == userId);

            if (notification == null || notification.IsRead)
            {
                return;
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var notifications = await _context.ReminderNotifications
                .Where(r => r.UserId == userId && !r.IsRead)
                .ToListAsync();

            if (notifications.Count == 0)
            {
                return;
            }

            foreach (var item in notifications)
            {
                item.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<(bool Success, string? Error)> SendTestEmailAsync(int userId, CancellationToken ct = default)
        {
            if (!_emailSender.IsConfigured)
            {
                return (false, "SMTP chua duoc cau hinh hoac chua bat (Smtp.Enabled/Host/FromEmail).");
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                return (false, "Khong tim thay user.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return (false, "Tai khoan nay chua co Email. Hay dang ky tai khoan moi co Email.");
            }

            try
            {
                await _emailSender.SendReminderEmailAsync(
                    user.Email,
                    "Test SMTP",
                    $"Email test tu Smart Scheduler luc {DateTime.Now:HH:mm dd/MM/yyyy}.");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP test failed for user {UserId}.", userId);
                return (false, ex.Message);
            }
        }
    }

    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;

        public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var reminderService = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    var created = await reminderService.ProcessDueRemindersAsync(stoppingToken);
                    if (created > 0)
                    {
                        _logger.LogInformation("Created {Count} reminder notifications.", created);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder background service failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
