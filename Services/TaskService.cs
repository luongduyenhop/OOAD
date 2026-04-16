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

        // Quản lý Categories
        Task<List<Category>> GetCategoriesAsync();
        Task AddCategoryAsync(string name);
        Task DeleteCategoryAsync(int categoryId);

        // Thống kê
        Task<Dictionary<AppTaskStatus, int>> GetStatusCountsAsync(int userId);
    }

    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Category?> GetOrCreateCategoryAsync(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name == name);
            if (category == null)
            {
                category = new Category(name);
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
            }
            return category;
        }

        public async Task AddCategoryAsync(string name)
        {
            if (!await _context.Categories.AnyAsync(c => c.Name == name))
            {
                _context.Categories.Add(new Category(name));
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteCategoryAsync(int categoryId)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category != null)
            {
                // OOAD: Khi xóa category, các task thuộc category đó sẽ bị set null CategoryId
                var tasks = await _context.Tasks.Where(t => t.CategoryId == categoryId).ToListAsync();
                foreach (var t in tasks) t.CategoryId = null;

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<AppTaskStatus, int>> GetStatusCountsAsync(int userId)
        {
            var counts = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);

            // Đảm bảo tất cả enum đều có mặt trong dictionary
            foreach (AppTaskStatus status in Enum.GetValues(typeof(AppTaskStatus)))
            {
                if (!counts.ContainsKey(status)) counts[status] = 0;
            }
            return counts;
        }
   
        public async Task CreateTaskAsync(int userId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime)
        {
            var category = await GetOrCreateCategoryAsync(categoryName);
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
            var query = _context.Tasks.Include(t => t.Category).Where(t => t.UserId == userId);

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
            return await _context.Tasks.Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);
        }

        public async Task UpdateTaskAsync(int userId, int taskId, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority, TaskFrequency frequency, DateTime? reminderTime, AppTaskStatus? status)
        {
            var existingTask = await GetTaskByIdAsync(taskId, userId);
            if (existingTask == null) return;

            string currentType = existingTask is RecurringTask ? "Recurring" : "Simple";
            var category = await GetOrCreateCategoryAsync(categoryName);

            if (currentType != type)
            {
                _context.Tasks.Remove(existingTask);
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

                var dayInstance = new SimpleTask { 
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
            if (task != null)
            {
                _context.Tasks.Remove(task);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<Category>> GetCategoriesAsync() => await _context.Categories.AsNoTracking().ToListAsync();
    }
}
