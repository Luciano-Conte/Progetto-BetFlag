using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BetFlag.BackEnd.Scommesse.Models
{
    public class BetRequest
    {
        // Identificativo univoco dell'utente
        [JsonIgnore] // Questo nasconde il campo UserId da Swagger e dal JSON in entrata
        public int UserId { get; set; }
        // Identificativo dell'evento (es. Inter-Milan)
        public int EventId { get; set; }
        // Il segno scelto: "1", "X" o "2"
        public string Sign { get; set; }
        // L'importo scommesso (es. 10.00€)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        // La quota offerta al momento della giocata (es. 2.50)
        [Column(TypeName = "decimal(18,2)")]
        public decimal Odds { get; set; }
    }
}
