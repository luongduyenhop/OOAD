using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace SchedulingApp.Models
{
    public enum AppTaskStatus
    {
        Created,
        InProgress,
        Completed,
        Overdue,
        Archived
    }

    public enum TaskFrequency
    {
        None,
        Daily,
        Weekly,
        Monthly,
        [Display(Name = "Monday-Friday")]
        Monday_Friday,
        Weekend
    }

    public class Category
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Category() { }
        public Category(string name) => Name = name;
    }

    public abstract class AppTask
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        public int UserId { get; set; }

        public AppTaskStatus Status { get; set; } = AppTaskStatus.Created;
        public DateTime? ReminderTime { get; set; }

        public abstract string GetDetails();
        public abstract IEnumerable<DateTime> GetOccurrences(DateTime rangeStart, DateTime rangeEnd);

        // OOAD: Đóng gói logic chuyển đổi trạng thái (State Machine)
        public IEnumerable<AppTaskStatus> GetAvailableTransitions()
        {
            if (Status == AppTaskStatus.Archived)
            {
                return Enumerable.Empty<AppTaskStatus>();
            }

            var transitions = new List<AppTaskStatus>();

            switch (Status)
            {
                case AppTaskStatus.Created:
                case AppTaskStatus.Overdue:
                    transitions.Add(AppTaskStatus.InProgress);
                    transitions.Add(AppTaskStatus.Completed);
                    break;
                case AppTaskStatus.InProgress:
                    transitions.Add(AppTaskStatus.Completed);
                    break;
                case AppTaskStatus.Completed:
                    transitions.Add(AppTaskStatus.InProgress);
                    break;
            }

            transitions.Add(AppTaskStatus.Archived);
            return transitions;
        }

        public bool CheckOverdue()
        {
            if (Status != AppTaskStatus.Completed && Status != AppTaskStatus.Archived &&
                Status != AppTaskStatus.Overdue && DateTime < DateTime.Now)
            {
                Status = AppTaskStatus.Overdue;
                return true;
            }
            return false;
        }
    }

    public class SimpleTask : AppTask
    {
        public override string GetDetails() =>
            $"Công việc: {Title} vào {DateTime:HH:mm dd/MM/yyyy} ({Category?.Name}) - Trạng thái: {Status}";

        public override IEnumerable<DateTime> GetOccurrences(DateTime rangeStart, DateTime rangeEnd)
        {
            if (DateTime >= rangeStart && DateTime <= rangeEnd)
                yield return DateTime;
        }
    }

    public class RecurringTask : AppTask
    {
        public TaskFrequency Frequency { get; set; } = TaskFrequency.Daily;

        public string? ExcludedDates { get; set; }

        public override string GetDetails() =>
            $"[LẶP LẠI {Frequency}] {Title} lúc {DateTime:HH:mm} ({Category?.Name}) - Trạng thái: {Status}";

        public override IEnumerable<DateTime> GetOccurrences(DateTime rangeStart, DateTime rangeEnd)
        {
            DateTime current = DateTime;
            DateTime limit = DateTime.AddYears(1) < rangeEnd ? DateTime.AddYears(1) : rangeEnd;
            
            var excludedList = string.IsNullOrEmpty(ExcludedDates) 
                ? new List<string>() 
                : ExcludedDates.Split(',').ToList();

            while (current <= limit)
            {
                if (current >= rangeStart)
                {
                    if (!excludedList.Contains(current.ToString("yyyy-MM-dd")))
                    {
                        bool shouldAdd = Frequency switch
                        {
                            TaskFrequency.Monday_Friday => current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday,
                            TaskFrequency.Weekend => current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday,
                            _ => true
                        };
                        if (shouldAdd) yield return current;
                    }
                }
                current = Frequency switch
                {
                    TaskFrequency.Daily or TaskFrequency.Monday_Friday or TaskFrequency.Weekend => current.AddDays(1),
                    TaskFrequency.Weekly => current.AddDays(7),
                    TaskFrequency.Monthly => current.AddMonths(1),
                    _ => limit.AddDays(1)
                };
            }
        }
    }

    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = string.Empty;
    }
}
