namespace BetFlag.BackEnd.Scommesse.Models
{
    public class Bets
    {
        public int Id { get; set; } // ID elemento nel db
        public int UserId { get; set; } // ID utente
        public int EventId { get; set; } // ID evento
        public string Sign { get; set; } // Il segno Es: "1", "X", "2"
        public decimal Amount { get; set; } // L'importo scommesso
        public decimal Odds { get; set; } // La quota offerta al momento della giocata
        public DateTime CreatedAt { get; set; } // Data scommessa
        public string Status { get; set; } // "Pending", "Processed", "Failed"
    }
}
