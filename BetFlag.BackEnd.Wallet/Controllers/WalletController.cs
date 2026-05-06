using BetFlag.BackEnd.Wallet.Data;
using BetFlag.BackEnd.Wallet.Models;
using Microsoft.AspNetCore.Mvc;

namespace BetFlag.BackEnd.Wallet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WalletController : ControllerBase
    {
        private readonly WalletDbContext _context;

        public WalletController(WalletDbContext context) => _context = context;

        [HttpGet("balance/{userId}")]
        public async Task<IActionResult> GetBalance(int userId)
        {
            var wallet = await _context.Wallets.FindAsync(userId);
            return wallet != null ? Ok(new { balance = wallet.Balance }) : NotFound();
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest req)
        {
            var wallet = await _context.Wallets.FindAsync(req.UserId);
            if (wallet == null) return NotFound();

            wallet.Balance += req.Amount;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deposito effettuato!", newBalance = wallet.Balance });
        }
    }
}
