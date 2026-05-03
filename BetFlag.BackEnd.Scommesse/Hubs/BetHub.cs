using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BetFlag.BackEnd.Scommesse.Hubs;

// L'Hub è il "punto di ritrovo" per i client connessi
[Authorize]
public class BetHub : Hub
{
    // Quando un client si connette, possiamo loggarlo
    public override Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "Anonimo";

        Console.WriteLine($"[SignalR] Utente ID: {userId} connesso. ConnectionId: {Context.ConnectionId}");

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? "Anonimo";
        Console.WriteLine($"[SignalR] Utente ID: {userId} disconnesso.");

        return base.OnDisconnectedAsync(exception);
    }
}
