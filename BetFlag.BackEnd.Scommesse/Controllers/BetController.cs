using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Hubs;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BetFlag.BackEnd.Scommesse.Controllers
{
    [ApiController] // Indica che questa classe gestisce API
    [Route("api/[controller]")] // L'URL sarà: api/bet
    public class BetController : ControllerBase
    {
        private readonly IBetService _betService;
        private readonly ApplicationDbContext _context; // Aggiunto per aggiornare lo stato della scommessa
        private readonly IHubContext<NotificationHub> _hubContext; // Per SignalR

        // Dependency Injection: .NET passa automaticamente l'istanza di BetService qui
        public BetController(IBetService betService, ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _betService = betService;
            _context = context;
            _hubContext = hubContext;
        }

        [HttpPost("place")] // Azione attivata da una chiamata POST a api/bet/place
        public async Task<IActionResult> PlaceBet([FromBody] BetRequest request)
        {
            // 1. Chiamiamo il servizio che abbiamo creato prima
            bool success = await _betService.PlaceBetAsync(request);

            if (!success)
            {
                // Restituiamo un errore 400 con un messaggio chiaro
                return BadRequest(new { message = "Il saldo è insufficiente o la scommessa non è valida" });
            }

            // Restituiamo un successo 200
            return Ok(new { message = "Scommessa registrata con successo!", userId = request.UserId });
        }

        [HttpPost("confirm")] // Il Worker chiama questo endpoint
        public async Task<IActionResult> ConfirmBet([FromBody] BetConfirmRequest confirmation)
        {
            // 1. Cerchiamo la scommessa nel database tramite l'ID ricevuto dal Worker
            var scommessa = await _context.Bets.FindAsync(confirmation.BetId);

            if (scommessa != null)
            {
                // 2. Aggiorniamo lo stato
                scommessa.Status = "Processed";
                await _context.SaveChangesAsync();

                // 3. Inviamo la notifica SignalR al browser
                await _hubContext.Clients.All.SendAsync("ReceiveBetNotification",
                    $"✅ Scommessa {scommessa.Id} confermata per l'utente {scommessa.UserId}!");
                return Ok();
            }
            return NotFound();
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetHistory(int userId)
        {
            var history = await _betService.GetUserBetHistoryAsync(userId);

            if (history == null || !history.Any())
            {
                return NotFound(new { message = $"Nessuna scommessa trovata per l'utente {userId}" });
            }
            return Ok(history);
        }
    }
}
