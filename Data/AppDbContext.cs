using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchedulingApp.Models;

namespace SchedulingApp.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AppTask> Tasks { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<ReminderNotification> ReminderNotifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppTask>()
                .HasDiscriminator<string>("TaskType")
                .HasValue<SimpleTask>("Simple")
                .HasValue<RecurringTask>("Recurring");

            modelBuilder.Entity<RecurringTask>()
                .Property(t => t.Frequency)
                .HasConversion(
                    v => v == TaskFrequency.Monday_Friday ? "Monday-Friday" : v.ToString(),
                    v => v == "Monday-Friday" ? TaskFrequency.Monday_Friday : (TaskFrequency)Enum.Parse(typeof(TaskFrequency), v));

            modelBuilder.Entity<AppTask>()
                .HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey("CategoryId");

            modelBuilder.Entity<Category>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.UserId, c.Name })
                .IsUnique();

            modelBuilder.Entity<AppTask>()
                .HasIndex(t => new { t.UserId, t.DateTime });
            modelBuilder.Entity<AppTask>()
                .HasIndex(t => new { t.UserId, t.Status });
            modelBuilder.Entity<AppTask>()
                .HasIndex(t => new { t.UserId, t.CategoryId });
            modelBuilder.Entity<AppTask>()
                .HasIndex(t => new { t.UserId, t.Priority });

            modelBuilder.Entity<ReminderNotification>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReminderNotification>()
                .HasOne(r => r.Task)
                .WithMany()
                .HasForeignKey(r => r.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReminderNotification>()
                .HasIndex(r => new { r.UserId, r.IsRead });

            modelBuilder.Entity<ReminderNotification>()
                .HasIndex(r => new { r.TaskId, r.ReminderTime })
                .IsUnique();

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.FullName)
                    .HasMaxLength(200);
            });
        }
    }
}
