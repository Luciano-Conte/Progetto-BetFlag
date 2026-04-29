using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace BetFlag.BackEnd.Consumer;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    // Questo metodo si avvia da solo all'accensione del microservizio
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. CICLO DI CONNESSIONE
        while (!stoppingToken.IsCancellationRequested && _connection == null)
        {
            try
            {
                // Creiamo la connessione asincrona
                _logger.LogInformation("🔄 Tentativo di connessione a RabbitMQ...");
                var factory = new ConnectionFactory()
                {
                    HostName = "queue-rabbitmq",
                };

                // IMPORTANTE: Assegna alla variabile di classe _connection
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                // IMPORTANTE: Crea il canale subito dopo la connessione
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            }
            catch (Exception)
            {
                _logger.LogWarning("⚠️ RabbitMQ non è ancora pronto o errore di rete. Riprovo tra 5 secondi...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        if (_channel == null) return;

        // 2. CONFIGURAZIONE CODA
        // Dichiariamo la coda (se non esiste, la crea. Se esiste, si aggancia)
        await _channel.QueueDeclareAsync(queue: "bet_queue",
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        _logger.LogInformation("✅ Connesso! In attesa di nuove scommesse...");

        // 3. CONFIGURAZIONE CONSUMER
        // Creiamo il "Consumer" (il lettore) asincrono
        var consumer = new AsyncEventingBasicConsumer(_channel);

        // Cosa fare quando arriva un messaggio?
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = System.Text.Encoding.UTF8.GetString(body);

            _logger.LogInformation($"[📥] NUOVA SCOMMESSA RICEVUTA: {message}");

            // Estrazione BetId
            int betId = 0;
            try
            {
                // Leggiamo l'ID dal JSON che arriva dall'API
                var data = JsonSerializer.Deserialize<JsonElement>(message);
                betId = data.GetProperty("BetId").GetInt32();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Errore durante la lettura del BetId: {ex.Message}");
            }

            // Simuliamo il tempo necessario per e validare la scommessa..
            await Task.Delay(2000, stoppingToken); // 2 secondi
            _logger.LogInformation($"[✅] Scommessa #{betId} elaborata!\n");

            // Avvisiamo l'API che abbiamo finito
            try
            {
                using var httpClient = new HttpClient();
                // Nota: "bet-api" è il nome del container nel docker-compose

                // Creiamo il payload che l'API si aspetta (BetConfirmRequest)
                var confirmPayload = new { BetId = betId };

                // Lo trasformiamo in JSON in modo sicuro
                var json = System.Text.Json.JsonSerializer.Serialize(confirmPayload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation($"🚀 Invio conferma per Scommessa #{betId} all'API...");

                var response = await httpClient.PostAsync("http://bet-api:8080/api/bet/confirm", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"✅ Scommessa #{betId} confermata con successo sul DB!");
                }
                else
                {
                    _logger.LogError($"❌ L'API ha risposto con errore: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {

                _logger.LogError($"❌ Errore di rete durante l'invio notifica: {ex.Message}");
            }
        };

        // Diciamo al canale di iniziare ad ascoltare usando il nostro consumer
        await _channel.BasicConsumeAsync(queue: "bet_queue",
            autoAck: true,
            consumer: consumer,
            cancellationToken: stoppingToken);

        // 4. ATTESA INFINITA (Mantiene il servizio attivo)
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Servizio in arresto...");
        }
    }

    // Pulizia quando spegniamo il container
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chiusura connessioni RabbitMQ...");
        if (_channel != null) await _channel.CloseAsync(cancellationToken);
        if (_connection != null) await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}