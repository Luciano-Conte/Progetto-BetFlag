using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Hubs;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using BetFlag.BackEnd.Scommesse.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. LEGGI LA STRINGA DI CONNESSIONE DALLE CONFIGURAZIONI (appsettings o variabili d'ambiente)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

// 2. REGISTRA I SERVIZI
// Add services to the container.
builder.Services.AddScoped<IBetService, BetServices>();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Configurazione OpenAPI e Swagger
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)
    ));

// Redis (Connessione sicura "Lazy" per evitare crash all'avvio)
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

// Configurazione Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "BetFlag_";
});

// Configurazione CORS specifica per SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true) // Consente connessioni da qualsiasi origine (es. il desktop)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // FONDAMENTALE per i WebSockets/SignalR
    });
});

var app = builder.Build();

// 3. MIDDLEWARE PIPELINE
app.UseRouting();
app.UseCors("SignalRPolicy");

// Forza l'apertura di Swagger anche in Docker
app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles(); // Cerca automaticamente index.html
app.UseStaticFiles();  // Abilita i file nella cartella wwwroot

app.UseAuthorization();

app.MapControllers();
app.MapHub<BetHub>("/bethub");

// 4. SEEDING INIZIALE (Database e Cache)
// Questo blocco crea il database e le tabelle all'avvio dell'app
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var _logger = services.GetRequiredService<ILogger<Program>>();

    // --- SEED SQL SERVER ---
    // Tentiamo di connetterci più volte perché SQL Server in Docker è lento ad avviarsi
    bool dbPronto = false;
    int tentativi = 0;

    while (!dbPronto && tentativi < 10)
    {
        try
        {
            tentativi++;
            using var context = services.GetRequiredService<ApplicationDbContext>();

            _logger.LogInformation("Tentativo {tentativo}: Verifica connessione SQL Server...", tentativi);

            // Crea il database se non esiste (utile per i test in Docker)
            context.Database.EnsureCreated();
            _logger.LogInformation("✅ Database SQL pronto!");

            // Inserisce un utente se non esiste
            if (!context.Users.Any())
            {
                context.Users.Add(new User { Username = "Lucia", Balance = 100.00m });
                context.SaveChanges();
                _logger.LogInformation("✅ Seed Utente completato!");
            }
            dbPronto = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ SQL Server non è ancora pronto (Tentativo {tentativo}/10). Attendo 5 secondi...", tentativi);
            Thread.Sleep(5000); // Aspetta 5 secondi prima di riprovare
        }
    }

    if (!dbPronto)
    {
        _logger.LogCritical("❌ IMPOSSIBILE CONNETTERSI A SQL SERVER DOPO 10 TENTATIVI.");
    }

    // --- SEED REDIS ---
    try
    {
        // Inserisce in redis una quota per un evento
        var multiplexer = services.GetRequiredService<IConnectionMultiplexer>();
        var redisDb = multiplexer.GetDatabase();
        if (!redisDb.KeyExists("quota:evento:1"))
        {
            redisDb.StringSet("quota:evento:1", "2.5");
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Errore durante il popolamento di Redis.");
    }
}

app.Run();