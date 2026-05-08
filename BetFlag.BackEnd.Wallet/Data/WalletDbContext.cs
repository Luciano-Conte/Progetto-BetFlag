using BetFlag.BackEnd.Wallet.Models;
using Microsoft.EntityFrameworkCore;

namespace BetFlag.BackEnd.Wallet.Data
{
    public class WalletDbContext : DbContext
    {
        public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }
        public DbSet<UserWallet> Wallets => Set<UserWallet>();
        public DbSet<ProcessedTransaction> ProcessedTransactions => Set<ProcessedTransaction>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserWallet>()
                .Property(w => w.Balance)
                .HasPrecision(18, 2);
        }
    }
}
