using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.EntityFrameworkCore;

namespace BetFlag.BackEnd.Scommesse.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options) { }

        // Rappresenta la tabella Users nel database
        public DbSet<User> Users { get; set; }
        public DbSet<Bets> Bets { get; set; }
    }
}
