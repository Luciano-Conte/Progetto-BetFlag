namespace BetFlag.BackEnd.Wallet.Models
{
    public class PaymentRequest
    {
        public int BetId { get; set; }
        public decimal Amount { get; set; }
    }
}