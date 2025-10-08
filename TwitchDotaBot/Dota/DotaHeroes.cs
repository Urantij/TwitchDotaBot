using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace TwitchDotaBot.Dota;

[JsonSerializable(typeof(HeroModel[]))]
public partial class HeroesSerializerContext : JsonSerializerContext
{
}

public class HeroModel
{
    [JsonPropertyName("id")] public int Id { get; }
    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("localized_name")] public string? LocalizedName { get; }

    public HeroModel(int id, string name, string? localizedName)
    {
        Id = id;
        Name = name;
        LocalizedName = localizedName;
    }
}

public class DotaHeroesConfig
{
    public string FilePath { get; } = "./DotaHeroes.json";
}

public class DotaHeroes : IHostedService
{
    private readonly ILogger<DotaHeroes> _logger;
    private readonly DotaHeroesConfig _config;

    private HeroModel[] _heroes = [];

    public DotaHeroes(IOptions<DotaHeroesConfig> options, ILogger<DotaHeroes> logger)
    {
        _logger = logger;
        this._config = options.Value;
    }

    public bool HasAny() => _heroes.Length > 0;

    public HeroModel? FindHero(int id)
    {
        return _heroes.FirstOrDefault(h => h.Id == id);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_config.FilePath))
        {
            _logger.LogWarning("Файла героев нет, не загружаем.");
            return;
        }

        try
        {
            HeroModel[] heroes = await LoadHeroesAsync();

            _heroes = heroes;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось загрузить героев.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    async Task<HeroModel[]> LoadHeroesAsync()
    {
        string content = await File.ReadAllTextAsync(_config.FilePath);

        return JsonSerializer.Deserialize<HeroModel[]>(content, new JsonSerializerOptions()
        {
            TypeInfoResolver = HeroesSerializerContext.Default
        })!;
    }
}