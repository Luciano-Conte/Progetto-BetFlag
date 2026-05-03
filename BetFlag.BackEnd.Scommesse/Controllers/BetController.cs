using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Hubs;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BetFlag.BackEnd.Scommesse.Controllers
{
    [Authorize] // <-- Ora NESSUNO può chiamare gli endpoint di scommessa senza Token!
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

        // Helper per ottenere l'ID utente dal Token senza ripetere il codice
        private int CurrentUserId => int.Parse(User.FindFirst("UserId")?.Value ?? "0");

        [HttpPost("place")] // Azione attivata da una chiamata POST a api/bet/place
        public async Task<IActionResult> PlaceBet([FromBody] BetRequest request)
        {
            // SICUREZZA: Ignoriamo l'ID che arriva dal JSON e usiamo quello del Token
            // In questo modo nessuno può "rubare" l'identità di un altro cambiando il JSON
            request.UserId = CurrentUserId;

            // Chiamiamo il servizio che abbiamo creato prima
            bool success = await _betService.PlaceBetAsync(request);

            if (!success)
            {
                // Restituiamo un errore 400 con un messaggio chiaro
                return BadRequest(new { message = "Il saldo è insufficiente o la scommessa non è valida" });
            }

            // Restituiamo un successo 200
            return Ok(new { message = "Scommessa registrata con successo!", userId = request.UserId });
        }

        [AllowAnonymous] // <-- Permette al Worker di confermare senza Token
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

                // 3. Notifica SignalR: inviamo il messaggio a un utente specifico
                await _hubContext.Clients.User(scommessa.UserId.ToString()).SendAsync("ReceiveBetNotification",
                    $"✅ Scommessa {scommessa.Id} confermata!");
                return Ok();
            }
            return NotFound();
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            // L'utente chiede "la mia cronologia", quindi usiamo il suo ID dal token
            var history = await _betService.GetUserBetHistoryAsync(CurrentUserId);

            if (history == null || !history.Any())
            {
                return NotFound(new { message = "Nessuna scommessa trovata nel tuo storico." });
            }
            return Ok(history);
        }
    }
}
