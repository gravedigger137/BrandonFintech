using Microsoft.EntityFrameworkCore;
using BrandonFintech.Identity;
using BrandonFintech.Accounts;
using BrandonFintech.Ledger;
using BrandonFintech.Payments;
using BrandonFintech.Transfers;
using BrandonFintech.Audit;

namespace BrandonFintech.Infrastructure;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyKey>()
            .HasIndex(x => new { x.UserId, x.Endpoint, x.Key })
            .IsUnique();
    }
}
