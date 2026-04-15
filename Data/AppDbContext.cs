using System;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchedulingApp.Models;

namespace SchedulingApp.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AppTask> Tasks { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình kế thừa TPH (Table-per-Hierarchy) - OOAD: Inheritance Mapping
            modelBuilder.Entity<AppTask>()
                .HasDiscriminator<string>("TaskType")
                .HasValue<SimpleTask>("Simple")
                .HasValue<RecurringTask>("Recurring");

            // Chuyển đổi Enum TaskFrequency sang String khi lưu vào DB (OOAD: Data Compatibility)
            modelBuilder.Entity<RecurringTask>()
                .Property(t => t.Frequency)
                .HasConversion(
                    v => v == TaskFrequency.Monday_Friday ? "Monday-Friday" : v.ToString(),
                    v => v == "Monday-Friday" ? TaskFrequency.Monday_Friday : (TaskFrequency)Enum.Parse(typeof(TaskFrequency), v));

            // Thiết lập quan hệ
            modelBuilder.Entity<AppTask>()
                .HasOne(t => t.Category)
                .WithMany()
                .HasForeignKey("CategoryId");

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(u => u.FullName)
                    .HasMaxLength(200);
            });
        }
    }
}
