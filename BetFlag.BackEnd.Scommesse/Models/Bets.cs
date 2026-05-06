using System.ComponentModel.DataAnnotations.Schema;

namespace BetFlag.BackEnd.Scommesse.Models
{
    public class Bets
    {
        public int Id { get; set; } // ID elemento nel db
        public int UserId { get; set; } // ID utente
        public int EventId { get; set; } // ID evento
        public string Sign { get; set; } // Il segno Es: "1", "X", "2"
        [Column(TypeName = "decimal(18,2)")] // Aggiunto per fixare il warning di EF Core
        public decimal Amount { get; set; } // L'importo scommesso
        [Column(TypeName = "decimal(18,2)")] // Aggiunto per fixare il warning di EF Core
        public decimal Odds { get; set; } // La quota offerta al momento della giocata
        public DateTime CreatedAt { get; set; } // Data scommessa
        public string Status { get; set; } // "Pending", "Processed", "Failed"
    }
}
