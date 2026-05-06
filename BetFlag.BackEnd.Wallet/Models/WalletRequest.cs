namespace BetFlag.BackEnd.Wallet.Models
{
    public class WalletRequest
    {
        public int BetId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
    }
}
