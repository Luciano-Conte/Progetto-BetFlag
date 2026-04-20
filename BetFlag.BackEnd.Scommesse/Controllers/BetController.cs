using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.AspNetCore.Mvc;

namespace BetFlag.BackEnd.Scommesse.Controllers
{
    [ApiController] // Indica che questa classe gestisce API
    [Route("api/[controller]")] // L'URL sarà: api/bet
    public class BetController : ControllerBase
    {
        private readonly IBetService _betService;

        // Dependency Injection: .NET passa automaticamente l'istanza di BetService qui
        public BetController(IBetService betService)
        {
            _betService = betService;
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
    }
}
