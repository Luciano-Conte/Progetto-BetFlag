using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Data;
using System.Text;
using System.Text.Json;

namespace BetFlag.BackEnd.Scommesse.Services
{
    public class BetServices : IBetService
    {
        private readonly ApplicationDbContext _context;
        private readonly StackExchange.Redis.IDatabase _redis;

        public BetServices(ApplicationDbContext context, IConnectionMultiplexer redis)
        {
            _context = context;
            _redis = redis.GetDatabase();
        }

        public async Task<IEnumerable<Bets>> GetUserBetHistoryAsync(int userId)
        {
            return await _context.Bets
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> PlaceBetAsync(BetRequest request)
        {
            // --- STEP 1: VERIFICA QUOTA SU REDIS ---
            // Supponiamo che le quote siano salvate con chiave "quota:evento:123"
            string redisKey = $"quota:evento:{request.EventId}";
            var currentOdds = _redis.StringGet(redisKey);

            if (!currentOdds.HasValue)
            {
                return false; // Quota non trovata o scaduta
            }

            decimal officialOdds = (decimal)currentOdds;

            // Se la quota è cambiata (es. era 2.15 e ora è 2.10), rifiutiamo la giocata
            if (officialOdds != request.Odds)
            {
                return false; // "Quota cambiata, accetta il nuovo valore?"
            }

            // --- STEP 2: SALVATAGGIO SCOMMESSA ---
            var nuovaScommessa = new Bets
            {
                UserId = request.UserId,
                EventId = request.EventId,
                Sign = request.Sign,
                Amount = request.Amount,
                Odds = request.Odds,
                CreatedAt = DateTime.UtcNow,
                Status = "Pending" // Rimane in attesa finché il Wallet non dice "OK"
            };

            _context.Bets.Add(nuovaScommessa);
            await _context.SaveChangesAsync();

            // --- STEP 3: CHIEDIAMO AL WALLET DI SCALARE I SOLDI ---
            try
            {
                var factory = new ConnectionFactory() { HostName = "queue-rabbitmq" };
                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(queue: "wallet_requests",
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                // Creiamo il payload esatto che il WalletWorker si aspetta di leggere
                var walletRequest = new
                {
                    BetId = nuovaScommessa.Id,
                    UserId = request.UserId,
                    Amount = request.Amount
                };

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(walletRequest));

                await channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: "wallet_requests",
                    body: body);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATTENZIONE] Errore invio richiesta al Wallet: {ex.Message}");
            }

            return true; // Diciamo all'utente "Scommessa presa in carico!"
        }
    }
}
