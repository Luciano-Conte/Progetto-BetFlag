using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace BetFlag.BackEnd.Scommesse.Providers
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            // Diciamo a SignalR di usare il nostro claim personalizzato "UserId"
            return connection.User?.FindFirst("UserId")?.Value;
        }
    }
}
