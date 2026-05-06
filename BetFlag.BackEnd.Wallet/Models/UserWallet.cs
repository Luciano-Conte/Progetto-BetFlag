namespace BetFlag.BackEnd.Wallet.Models
{
    public class UserWallet
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }
}
