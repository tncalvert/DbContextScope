using DbContextScope.DemoConsoleApp.DomainModel;
using Microsoft.EntityFrameworkCore;

namespace DbContextScope.DemoConsoleApp.DatabaseContext {
    public class UserManagementDbContext : DbContext {
        // Map our 'User' model by convention
        public DbSet<User> Users { get; set; }

        public UserManagementDbContext(DbContextOptions<UserManagementDbContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(builder => {
                builder.Property(m => m.Name).IsRequired();
                builder.Property(m => m.Email).IsRequired();
            });
        }
    }
}
