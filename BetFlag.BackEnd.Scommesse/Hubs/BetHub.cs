using Microsoft.AspNetCore.SignalR;

namespace BetFlag.BackEnd.Scommesse.Hubs;

// L'Hub è il "punto di ritrovo" per i client connessi
public class BetHub : Hub
{
    // Quando un client si connette, possiamo loggarlo
    public override Task OnConnectedAsync()
    {
        Console.WriteLine( $"[SignalR] Nuovo client connesso: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }
}
