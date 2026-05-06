using System;
using System.Collections.Generic;
using System.Text;

namespace BetFlag.BackEnd.Consumer.Models
{
    internal class WalletResponse
    {
        public int BetId { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}
