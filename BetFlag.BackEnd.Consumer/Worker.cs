using RabbitMQ.Client;
using BetFlag.BackEnd.Consumer.Models;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Net.Http;

namespace BetFlag.BackEnd.Consumer
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private const string QueueName = "bet_responses";

        public Worker(ILogger<Worker> logger, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        // Questo metodo si avvia da solo all'accensione del microservizio
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "queue-rabbitmq" };
            IConnection? connection = null;

            // --- FIX: Retry logic per RabbitMQ ---
            while (connection == null && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Tentativo di connessione a RabbitMQ...");
                    connection = await factory.CreateConnectionAsync();
                    _logger.LogInformation("✅ Connesso a RabbitMQ con successo!");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"RabbitMQ non ancora pronto. Riprovo tra 5 secondi... Dettaglio: {ex.Message}");
                    await Task.Delay(5000, stoppingToken); // Aspetta 5 secondi prima di riprovare
                }
            }

            if (connection == null) return; // Se il token è stato cancellato durante l'attesa, usciamo
            // --- FINE FIX ---

            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation(" [x] Risposta ricevuta dal Wallet: {0}", message);

                try
                {
                    // Deserializziamo la risposta del Wallet
                    var walletResponse = JsonSerializer.Deserialize<WalletResponse>(message);

                    if (walletResponse != null)
                    {
                        // Comunichiamo l'esito all'API Scommesse
                        await ConfirmBetToApi(walletResponse);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Errore processamento messaggio: {ex.Message}");
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: true, consumer: consumer);
            await Task.Delay(-1, stoppingToken); // Mantiene il worker in esecuzione
        }

        private async Task ConfirmBetToApi(WalletResponse response)
        {
            var client = _httpClientFactory.CreateClient();
            // L'API scommesse deve sapere se mettere "Processed" o "Rejected"
            var content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json");

            // Chiamiamo l'endpoint di conferma dell'API Scommesse
            var apiUrl = "http://bet-api:8080/api/bet/confirm";
            var result = await client.PostAsync(apiUrl, content);

            if (result.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Scommessa {response.BetId} gestita con successo (Stato: {response.Success})");
            }
            else
            {
                _logger.LogError($"Errore nel callback all'API per la scommessa {response.BetId}");
            }
        }
    }
}