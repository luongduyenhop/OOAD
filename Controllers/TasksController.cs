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

        public TasksController(ITaskService taskService, IAuthService authService)
        {
            _taskService = taskService;
            _authService = authService;
        }

        public async Task<IActionResult> Index(string? searchTerm, int? categoryId, AppTaskStatus? status)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            
            var tasks = await _taskService.GetTasksAsync(userId.Value, searchTerm, categoryId, status);
            ViewBag.Categories = await _taskService.GetCategoriesAsync();
            ViewBag.StatusCounts = await _taskService.GetStatusCountsAsync(userId.Value); // Gửi thống kê trạng thái
            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(string name)
        {
            await _taskService.AddCategoryAsync(name);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _taskService.DeleteCategoryAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string type, string categoryName, DateTime dateTime, TaskFrequency frequency = TaskFrequency.Daily, DateTime? reminderTime = null)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            
            await _taskService.CreateTaskAsync(userId.Value, title, type, categoryName, dateTime, frequency, reminderTime);
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

            return Json(new { 
                id = task.Id, title = task.Title, dateTime = task.DateTime.ToString("yyyy-MM-ddTHH:mm"),
                categoryName = task.Category?.Name,
                type = task is RecurringTask ? "Recurring" : "Simple",
                frequency = (task as RecurringTask)?.Frequency.ToString() ?? "None",
                status = task.Status.ToString(),
                reminderTime = task.ReminderTime?.ToString("yyyy-MM-ddTHH:mm"),
                transitions = task.GetAvailableTransitions().Select(t => t.ToString()) // Trả về danh sách hành động hợp lệ
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string title, string type, string categoryName, DateTime dateTime, TaskFrequency frequency = TaskFrequency.Daily, DateTime? reminderTime = null, AppTaskStatus? status = null)
        {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            
            await _taskService.UpdateTaskAsync(userId.Value, id, title, type, categoryName, dateTime, frequency, reminderTime, status);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id) {
            var userId = _authService.GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Account");
            await _taskService.DeleteTaskAsync(id, userId.Value);
            return RedirectToAction("Index");
        }
    }
}
