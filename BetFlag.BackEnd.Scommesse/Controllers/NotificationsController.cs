using BetFlag.BackEnd.Scommesse.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BetFlag.BackEnd.Scommesse.Controllers;

// Questa classe serve a "impacchettare" il messaggio in modo corretto
public class NotificationRequest
{
    public string Message { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IHubContext<BetHub> _hubContext;

    // Iniettiamo il contesto dell'Hub per poter parlare con i browser
    public NotificationsController(IHubContext<BetHub> hubContext)
    {
        _hubContext = hubContext;
    }

    [HttpPost("bet-completed")]
    public async Task<IActionResult> BetCompleted([FromBody] NotificationRequest request)
    {
        // Questo comando invia un messaggio a TUTTI i browser connessi
        // dicendogli di eseguire una funzione JavaScript chiamata "ReceiveBetNotification"
        await _hubContext.Clients.All.SendAsync("ReceiveBetNotification", request.Message);
        return Ok();
    }
}
