using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Data;

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

            // --- STEP 2: VERIFICA SALDO SU SQL SERVER ---
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null || user.Balance < request.Amount)
            {
                return false; // Utente non trovato o saldo insufficiente
            }

            // --- STEP 3: TRANSAZIONE ---
            // Logica di sottrazione saldo (Transazionale ACID)
            user.Balance -= request.Amount;
            await _context.SaveChangesAsync(); // Questo comando salva fisicamente su SQL Server

            // --- STEP 4: PUBBLICA IL MESSAGGIO SU RABBITMQ ---
            try
            {
                // Ci connettiamo al container RabbitMQ (il nome è quello nel docker-compose)
                var factory = new ConnectionFactory() { HostName = "queue-rabbitmq" };
                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();
                    // Dichiariamo una coda chiamata "bet_queue"
                    await channel.QueueDeclareAsync(queue: "bet_queue",
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    // Trasformiamo l'oggetto in JSON e poi in Byte (il formato richiesto da RabbitMQ)
                    var messageJson = JsonSerializer.Serialize(new {
                        UserId = request.UserId,
                        EventId = request.EventId,
                        Amount = request.Amount,
                        Timestamp = DateTime.UtcNow
                    });
                    var body = Encoding.UTF8.GetBytes(messageJson);

                    // Inviamo il messaggio nella coda
                    await channel.BasicPublishAsync(exchange: "",
                        routingKey: "bet_queue",
                        body: body);
            }
            catch (Exception ex)
            {
                // Se RabbitMQ cade, la scommessa è comunque valida (i soldi sono stati presi).
                // In un sistema reale, qui salveremmo l'errore in un log di emergenza.
                Console.WriteLine($"[ATTENZIONE] Errore invio a RabbitMQ: {ex.Message}");
            }

            return true;
        }
    }
}
