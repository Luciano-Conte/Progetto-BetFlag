using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace BetFlag.BackEnd.Scommesse.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IDistributedCache cache, ILogger<EventsController> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    [HttpGet("{eventId}/odds")]
    public async Task<IActionResult> GetOdds(int eventId)
    {
        string cacheKey = $"event_odds_{eventId}";

        // 1. Prova a leggere da Redis
        var cacheData = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cacheData))
        {
            _logger.LogInformation("⚡ [Redis] Cache HIT per l'evento {EventId}", eventId);
            var odds = JsonSerializer.Deserialize<decimal>(cacheData);
            return Ok(new { eventId = eventId, Odds = odds, Source = "Redis Cache" });
        }

        // 2. Se non c'è, simula la lettura dal Database (Cache Miss)
        _logger.LogWarning("🐢 [SQL] Cache MISS per l'evento {EventId}. Lettura dal DB...", eventId);

        // Simuliamo una quota che arriva dal DB
        decimal oddsFromDb = 2.10m;
        await Task.Delay(500); // Simula la lentezza del DB

        // 3. Salva il risultato in Redis con una scadenza (TTL: Time To Live) di 60 secondi
        var options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(60));

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(oddsFromDb), options);

        return Ok(new { EventId = eventId, Odds = oddsFromDb, Source = "SQL Server (e salvato in Cache)" });
    }
}