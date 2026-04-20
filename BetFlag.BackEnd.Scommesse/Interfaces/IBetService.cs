using BetFlag.BackEnd.Scommesse.Models;

namespace BetFlag.BackEnd.Scommesse.Interfaces
{
    public interface IBetService
    {
        // Restituisce un valore booleano: true se la giocata è valida, false altrimenti
        Task<bool> PlaceBetAsync(BetRequest request);
    }
}
