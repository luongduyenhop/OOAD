using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SchedulingApp.Data;
using SchedulingApp.Models;

namespace SchedulingApp.Services
{
    public interface ITaskService
    {
        Task CreateTaskAsync(int userId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime);
        Task<List<AppTask>> GetTasksAsync(int userId, string? searchTerm = null, int? categoryId = null, AppTaskStatus? status = null, TaskPriority? priority = null);
        Task<AppTask?> GetTaskByIdAsync(int taskId, int userId);
        Task UpdateTaskAsync(int userId, int taskId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime, AppTaskStatus? status);
        Task UpdateStatusAsync(int taskId, int userId, AppTaskStatus newStatus, DateTime? date = null);
        Task DeleteTaskAsync(int taskId, int userId);

        Task<List<Category>> GetCategoriesAsync(int userId);
        Task AddCategoryAsync(int userId, string name);
        Task DeleteCategoryAsync(int userId, int categoryId);

        Task<Dictionary<AppTaskStatus, int>> GetStatusCountsAsync(int userId);
    }

    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        private async Task EnsureDefaultCategoriesAsync(int userId)
        {
            var hasAny = await _context.Categories.AnyAsync(c => c.UserId == userId);
            if (hasAny) return;

            _context.Categories.AddRange(
                new Category("Công việc", userId),
                new Category("Cá nhân", userId),
                new Category("Học tập", userId));
            await _context.SaveChangesAsync();
        }

        private async Task<Category?> GetOrCreateCategoryAsync(int userId, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            await EnsureDefaultCategoriesAsync(userId);

            var normalized = name.Trim();
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Name == normalized);
            if (category == null)
            {
                category = new Category(normalized, userId);
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }

            return category;
        }

        public async Task<List<Category>> GetCategoriesAsync(int userId)
        {
            await EnsureDefaultCategoriesAsync(userId);
            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task AddCategoryAsync(int userId, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var normalized = name.Trim();
            var exists = await _context.Categories.AnyAsync(c => c.UserId == userId && c.Name == normalized);
            if (exists) return;

            _context.Categories.Add(new Category(normalized, userId));
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCategoryAsync(int userId, int categoryId)
        {
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId);
            if (category == null) return;

            var tasks = await _context.Tasks
                .Where(t => t.UserId == userId && t.CategoryId == categoryId)
                .ToListAsync();
            foreach (var t in tasks) t.CategoryId = null;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
        }

        public async Task<Dictionary<AppTaskStatus, int>> GetStatusCountsAsync(int userId)
        {
            var counts = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            foreach (AppTaskStatus status in Enum.GetValues(typeof(AppTaskStatus)))
            {
                if (!counts.ContainsKey(status)) counts[status] = 0;
            }
            return counts;
        }

        public async Task CreateTaskAsync(int userId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime)
        {
            var category = await GetOrCreateCategoryAsync(userId, categoryName);
            AppTask newTask = type == "Recurring"
                ? new RecurringTask { Frequency = frequency }
                : new SimpleTask();

            newTask.UserId = userId;
            newTask.Title = title;
            newTask.DateTime = dateTime;
            newTask.Category = category;
            newTask.Priority = priority;
            newTask.ReminderTime = reminderTime;

            _context.Tasks.Add(newTask);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AppTask>> GetTasksAsync(int userId, string? searchTerm = null, int? categoryId = null, AppTaskStatus? status = null, TaskPriority? priority = null)
        {
            var query = _context.Tasks
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (!string.IsNullOrEmpty(searchTerm)) query = query.Where(t => t.Title.Contains(searchTerm));
            if (categoryId.HasValue) query = query.Where(t => t.CategoryId == categoryId);
            if (status.HasValue) query = query.Where(t => t.Status == status);
            if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

            var tasks = await query.ToListAsync();

            bool hasChanges = false;
            foreach (var task in tasks)
            {
                if (task.CheckOverdue()) hasChanges = true;
            }

            if (hasChanges) await _context.SaveChangesAsync();

            return tasks;
        }

        public async Task<AppTask?> GetTaskByIdAsync(int taskId, int userId)
        {
            return await _context.Tasks
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        }

        public async Task UpdateTaskAsync(int userId, int taskId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime, AppTaskStatus? status)
        {
            var existingTask = await GetTaskByIdAsync(taskId, userId);
            if (existingTask == null) return;

            string currentType = existingTask is RecurringTask ? "Recurring" : "Simple";
            var category = await GetOrCreateCategoryAsync(userId, categoryName);

            if (currentType != type)
            {
                var reminders = await _context.ReminderNotifications
                    .Where(r => r.TaskId == taskId)
                    .ToListAsync();

                _context.Tasks.Remove(existingTask);
                await _context.SaveChangesAsync(); // Commit delete to free up the slot if needed (though EF handles identity)

                AppTask newTask = type == "Recurring"
                    ? new RecurringTask { Frequency = frequency }
                    : new SimpleTask();

                newTask.UserId = userId;
                newTask.Title = title;
                newTask.DateTime = dateTime;
                newTask.Category = category;
                newTask.Priority = priority;
                newTask.ReminderTime = reminderTime;
                if (status.HasValue) newTask.Status = status.Value;

                _context.Tasks.Add(newTask);
                await _context.SaveChangesAsync();

                // Update reminders to point to the new Task ID
                foreach (var r in reminders)
                {
                    r.TaskId = newTask.Id;
                }
            }
            else
            {
                existingTask.Title = title;
                existingTask.DateTime = dateTime;
                existingTask.Category = category;
                existingTask.Priority = priority;
                existingTask.ReminderTime = reminderTime;
                if (status.HasValue) existingTask.Status = status.Value;
                if (existingTask is RecurringTask rt) rt.Frequency = frequency;
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(int taskId, int userId, AppTaskStatus newStatus, DateTime? date = null)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return;
            if (!task.GetAvailableTransitions().Contains(newStatus)) return;

            if (task is RecurringTask rt && date.HasValue)
            {
                string dateStr = date.Value.ToString("yyyy-MM-dd");
                if (string.IsNullOrEmpty(rt.ExcludedDates)) rt.ExcludedDates = dateStr;
                else if (!rt.ExcludedDates.Contains(dateStr)) rt.ExcludedDates += "," + dateStr;

                var dayInstance = new SimpleTask
                {
                    Title = task.Title,
                    DateTime = date.Value,
                    CategoryId = task.CategoryId,
                    UserId = task.UserId,
                    Priority = task.Priority,
                    Status = newStatus,
                    ReminderTime = task.ReminderTime
                };
                _context.Tasks.Add(dayInstance);
            }
            else
            {
                task.Status = newStatus;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteTaskAsync(int taskId, int userId)
        {
            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
            if (task == null) return;

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
    }
}
