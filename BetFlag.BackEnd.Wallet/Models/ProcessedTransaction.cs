using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BetFlag.BackEnd.Wallet.Models
{
    public class ProcessedTransaction
    {
        [Key] // Usiamo il BetId come chiave primaria univoca
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int BetId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
