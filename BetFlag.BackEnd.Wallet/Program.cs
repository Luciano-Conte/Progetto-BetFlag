using BetFlag.BackEnd.Wallet.Data;
using BetFlag.BackEnd.Wallet.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Registra il Database
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)
        ));

// 2. Registra il Worker di RabbitMQ in modo che parta all'avvio
builder.Services.AddHostedService<WalletWorker>();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 3. Crea il database del Wallet all'avvio se non esiste
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WalletDbContext>();
    dbContext.Database.EnsureCreated();

    // Puoi inserire qui un seed per dare a Lucia il saldo iniziale nel db Wallet
    if (!dbContext.Wallets.Any())
    {
        dbContext.Wallets.Add(new BetFlag.BackEnd.Wallet.Models.UserWallet { Username = "Lucia", Balance = 100.00m });
        dbContext.SaveChanges();
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();