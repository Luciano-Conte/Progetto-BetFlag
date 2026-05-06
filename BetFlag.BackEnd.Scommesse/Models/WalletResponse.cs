namespace BetFlag.BackEnd.Scommesse.Models
{
    public class WalletResponse
    {
        public int BetId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
