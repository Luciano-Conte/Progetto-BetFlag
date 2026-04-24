using BetFlag.BackEnd.Scommesse.Data;
using BetFlag.BackEnd.Scommesse.Interfaces;
using BetFlag.BackEnd.Scommesse.Models;
using BetFlag.BackEnd.Scommesse.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// LEGGI LA STRINGA DI CONNESSIONE DALLE CONFIGURAZIONI (appsettings o variabili d'ambiente)
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
var multiplexer = ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false");

// Add services to the container.
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddScoped<IBetService, BetServices>();
builder.Services.AddControllers();

// Configurazione OpenAPI e Swagger
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

// Configurazione Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "BetFlag_";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Abilita l'interfaccia grafica Swagger
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

// Questo blocco crea il database e le tabelle all'avvio dell'app
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // --- SEED SQL SERVER ---
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        // Crea il database se non esiste (utile per i test in Docker)
        context.Database.EnsureCreated();

        // Inserisce un utente se non esiste
        if (!context.Users.Any())
        {
            context.Users.Add(new User { Username = "Lucia", Balance = 100.00m });
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Errore durante la creazione/popolamento del database SQL.");
    }

    // --- SEED REDIS ---
    try
    { 
        // Inserisce in redis una quota per un evento
        var redisDb = multiplexer.GetDatabase();
        if (!redisDb.KeyExists("quota:evento:1"))
        {
            redisDb.StringSet("quota:evento:1", "2.5");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Errore durante il popolamento di Redis.");
    }
}

app.Run();