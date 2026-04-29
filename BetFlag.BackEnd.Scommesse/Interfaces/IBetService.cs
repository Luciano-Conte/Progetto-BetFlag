using BetFlag.BackEnd.Scommesse.Models;

namespace BetFlag.BackEnd.Scommesse.Interfaces
{
    public interface IBetService
    {
        // Restituisce un valore booleano: true se la giocata è valida, false altrimenti
        Task<bool> PlaceBetAsync(BetRequest request);

        // Restituisce lo storico delle scommesse dell'utente
        Task<IEnumerable<Bets>> GetUserBetHistoryAsync(int userId);
    }
}
