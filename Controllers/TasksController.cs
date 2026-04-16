using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchedulingApp.Models;
using SchedulingApp.Services;

namespace SchedulingApp.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly ITaskService _taskService;
        private readonly IAuthService _authService;
        private readonly IReminderService _reminderService;

        public TasksController(ITaskService taskService, IAuthService authService, IReminderService reminderService)
        {
            _taskService = taskService;
            _authService = authService;
            _reminderService = reminderService;
        }

        public async Task<IActionResult> Index(string? searchTerm, int? categoryId, AppTaskStatus? status, TaskPriority? priority)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var tasks = await _taskService.GetTasksAsync(userId.Value, searchTerm, categoryId, status, priority);
            ViewBag.Categories = await _taskService.GetCategoriesAsync(userId.Value);
            ViewBag.StatusCounts = await _taskService.GetStatusCountsAsync(userId.Value);

            var unread = await _reminderService.GetUnreadNotificationsAsync(userId.Value, 10);
            ViewBag.ReminderNotifications = unread;
            ViewBag.UnreadReminderCount = unread.Count;

            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReminderRead(int id)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _reminderService.MarkAsReadAsync(id, userId.Value);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRemindersRead()
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _reminderService.MarkAllAsReadAsync(userId.Value);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestReminderEmail()
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            var result = await _reminderService.SendTestEmailAsync(userId.Value);
            TempData["EmailTest"] = result.Success
                ? "Gui email test thanh cong. Kiem tra Inbox/Spam."
                : $"Gui email test that bai: {result.Error}";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string name)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _taskService.AddCategoryAsync(userId.Value, name);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _taskService.DeleteCategoryAsync(userId.Value, id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string type, string categoryName, DateTime dateTime, TaskPriority priority = TaskPriority.Medium, TaskFrequency frequency = TaskFrequency.Daily, DateTime? reminderTime = null)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _taskService.CreateTaskAsync(userId.Value, title, type, categoryName, dateTime, priority, frequency, reminderTime);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, AppTaskStatus newStatus, DateTime? date = null)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            await _taskService.UpdateStatusAsync(id, userId.Value, newStatus, date);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> GetTask(int id)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var task = await _taskService.GetTaskByIdAsync(id, userId.Value);
            if (task == null) return NotFound();

            string statusLabel = task.Status switch
            {
                AppTaskStatus.Created => "Chưa bắt đầu",
                AppTaskStatus.InProgress => "Đang thực hiện",
                AppTaskStatus.Completed => "Hoàn thành",
                AppTaskStatus.Overdue => "Quá hạn",
                AppTaskStatus.Archived => "Lưu trữ",
                _ => task.Status.ToString()
            };

            return Json(new
            {
                id = task.Id,
                title = task.Title,
                dateTime = task.DateTime.ToString("yyyy-MM-ddTHH:mm"),
                categoryName = task.Category?.Name,
                type = task is RecurringTask ? "Recurring" : "Simple",
                frequency = (task as RecurringTask)?.Frequency.ToString() ?? "None",
                priority = task.Priority.ToString(),
                status = task.Status.ToString(),
                statusLabel,
                reminderTime = task.ReminderTime?.ToString("yyyy-MM-ddTHH:mm"),
                transitions = task.GetAvailableTransitions().Select(t => t.ToString())
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string title, string type, string categoryName, DateTime dateTime, TaskPriority priority = TaskPriority.Medium, TaskFrequency frequency = TaskFrequency.Daily, DateTime? reminderTime = null, AppTaskStatus? status = null)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _taskService.UpdateTaskAsync(userId.Value, id, title, type, categoryName, dateTime, priority, frequency, reminderTime, status);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");

            await _taskService.DeleteTaskAsync(id, userId.Value);
            return RedirectToAction("Index");
        }
    }
}
