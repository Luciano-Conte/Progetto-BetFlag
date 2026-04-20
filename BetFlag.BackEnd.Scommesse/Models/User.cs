using System.ComponentModel.DataAnnotations.Schema;

namespace BetFlag.BackEnd.Scommesse.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } // E' il "Saldo Vero" dell'utente
    }
}
