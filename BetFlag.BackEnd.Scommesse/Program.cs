using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Hubs;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using BetFlag.BackEnd.Scommesse.Services;
using BetFlag.BackEnd.Scommesse.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. LEGGI LA STRINGA DI CONNESSIONE DALLE CONFIGURAZIONI (appsettings o variabili d'ambiente)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

// 2. REGISTRA I SERVIZI
// Add services to the container.
builder.Services.AddScoped<IBetService, BetServices>();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Insegna a SignalR a usare l'ID dal Token JWT
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();

// Configurazione OpenAPI e Swagger
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
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

// 3. Configurazione Autenticazione JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(option =>
    {
        option.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Serve per leggere il token inviato da SignalR
        option.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                // Se la richiesta è per il nostro Hub e contiene un token, usalo!
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/bethub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// 4. Configurazione Swagger con supporto JWT
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BetFlag API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // RFC 7235 consiglia il minuscolo per lo scheme
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Inserisci il token JWT. Esempio: '12345abcdef'"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document), new List<string>()
        }
    });
});

var app = builder.Build();

// 5. MIDDLEWARE PIPELINE
app.UseRouting();
app.UseCors("SignalRPolicy");

// Forza l'apertura di Swagger anche in Docker
app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles(); // Cerca automaticamente index.html
app.UseStaticFiles();  // Abilita i file nella cartella wwwroot

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/bethub");

// 6. SEEDING INIZIALE (Database e Cache)
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
        catch (Exception)
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