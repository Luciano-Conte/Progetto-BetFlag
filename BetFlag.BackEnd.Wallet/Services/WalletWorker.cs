using Azure;
using BetFlag.BackEnd.Wallet.Data;
using BetFlag.BackEnd.Wallet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BetFlag.BackEnd.Wallet.Services
{
    public class WalletWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private IConnection? _connection;
        private IChannel? _channel;

        public WalletWorker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[WALLET WORKER] Avvio servizio e tentativo di connessione a RabbitMQ...");

            // --- INIZIO LOGICA DI RETRY ---
            int retryCount = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var factory = new ConnectionFactory { HostName = "queue-rabbitmq" };
                    _connection = await factory.CreateConnectionAsync(stoppingToken);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    Console.WriteLine("[WALLET WORKER] ✅ Connesso a RabbitMQ con successo!");
                    break; // Usciamo dal loop se la connessione ha successo
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Console.WriteLine($"[WALLET WORKER] ⚠️ RabbitMQ non ancora pronto. Tentativo {retryCount} tra 5 secondi... Errore: {ex.Message}");
                    await Task.Delay(5000, stoppingToken); // Aspetta 5 secondi prima di riprovare
                }
            }

            // Se l'applicazione si sta spegnendo e non abbiamo il canale, interrompiamo
            if (_channel == null) return;
            // --- FINE LOGICA DI RETRY ---

            // Dichiarazione code asincrona
            await _channel.QueueDeclareAsync(queue: "wallet_requests", durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(queue: "bet_responses", durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    var paymentRequest = JsonSerializer.Deserialize<PaymentRequest>(message);
                    if (paymentRequest == null) return;

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();

                        // ================================================================
                        // 1. 🛡️ CONTROLLO IDEMPOTENZA: Abbiamo già processato questo BetId?
                        // ================================================================
                        bool alreadyProcessed = dbContext.ProcessedTransactions.Any(pt => pt.BetId == paymentRequest.BetId);
                        if (alreadyProcessed)
                        {
                            Console.WriteLine($"[WALLET WORKER] 🛡️ TRANSAZIONE IDEMPOTENTE: BetId {paymentRequest.BetId} già processato. Ignoro l'addebito ma confermo il successo.");

                            // Rispondiamo comunque OK all'API per chiudere il cerchio nel caso il messaggio di risposta originale si fosse perso
                            var idempotentResponse = new { BetId = paymentRequest.BetId, Success = true, Status = "Processed", Message = "Pagamento già completato in precedenza" };
                            var idempotentBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(idempotentResponse));

                            await _channel.BasicPublishAsync(string.Empty, "bet_responses", idempotentBytes, stoppingToken);
                            return; // Usciamo per non scalare i soldi altre volte!
                        }

                        // ================================================================
                        // 2. ELABORAZIONE NORMALE
                        // ================================================================
                        var wallet = dbContext.Wallets.FirstOrDefault(w => w.Username == "Lucia");

                        bool isSuccess = false;
                        string status = "Rejected";
                        string note = "Saldo insufficiente";

                        if (wallet != null && wallet.Balance >= paymentRequest.Amount)
                        {
                            // Scala i soldi
                            wallet.Balance -= paymentRequest.Amount;

                            // Registra la transazione come completata nella stessa operazione di salvataggio
                            dbContext.ProcessedTransactions.Add(new ProcessedTransaction
                            {
                                BetId = paymentRequest.BetId,
                                ProcessedAt = DateTime.UtcNow
                            });

                            await dbContext.SaveChangesAsync(stoppingToken); // <-- Transazione atomica!

                            isSuccess = true;
                            status = "Processed";
                            note = "Pagamento completato";
                            Console.WriteLine($"[WALLET WORKER] Pagamento processato per BetId: {paymentRequest.BetId}. Nuovo saldo: {wallet.Balance}");
                        }
                        else
                        {
                            Console.WriteLine($"[WALLET WORKER] ❌ Saldo insufficiente o utente non trovato per BetId: {paymentRequest.BetId}");
                        }

                        // --- INVIA RISPOSTA ---
                        var response = new
                        {
                            BetId = paymentRequest.BetId,
                            Success = isSuccess,
                            Status = status,
                            Message = note
                        };

                        var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

                        await _channel.BasicPublishAsync(
                            exchange: string.Empty,
                            routingKey: "bet_responses",
                            body: responseBytes,
                            cancellationToken: stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    // Logga l'errore se la deserializzazione fallisce
                    Console.WriteLine($"[ERRORE WALLET] {ex.Message}");
                }
            };

            await _channel.BasicConsumeAsync(queue: "wallet_requests", autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

            // Mantieni il worker in vita finché non viene fermato
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}