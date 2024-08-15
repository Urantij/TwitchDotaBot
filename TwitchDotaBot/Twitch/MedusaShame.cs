using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;

namespace TwitchDotaBot.Twitch;

public class MedusaShameConfig
{
    public class Pair
    {
        public required uint HeroId { get; init; }
        public required string Message { get; init; }
    }

    public ICollection<Pair>? Lines { get; init; }
}

public class MedusaShame : IHostedService
{
    private readonly DotaClient _dota;
    private readonly ChatBot _chatBot;
    private readonly ILogger<MedusaShame> _logger;
    private readonly AppConfig _appConfig;
    private readonly MedusaShameConfig _config;

    private MatchModel? _lastShamedMatch;

    public MedusaShame(DotaClient dota, ChatBot chatBot, IOptions<AppConfig> appOptions,
        IOptions<MedusaShameConfig> options, ILogger<MedusaShame> logger)
    {
        _dota = dota;
        _chatBot = chatBot;
        _logger = logger;
        _appConfig = appOptions.Value;
        _config = options.Value;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("У нас {count} пар.", _config.Lines?.Count ?? 0);

        _dota.MatchUpdated += DotaOnMatchUpdated;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dota.MatchUpdated -= DotaOnMatchUpdated;

        return Task.CompletedTask;
    }

    private void DotaOnMatchUpdated(MatchModel obj)
    {
        if (obj.Id == _lastShamedMatch?.Id)
            return;

        PlayerModel? player = BaseCommand.GetPlayer(obj, _appConfig.SteamId);

        if (player == null)
            return;

        MedusaShameConfig.Pair? line = _config.Lines?.FirstOrDefault(l => l.HeroId == player.HeroId);

        if (line == null)
            return;

        _lastShamedMatch = obj;

        Task.Run(async () =>
        {
            try
            {
                await _chatBot.Channel.SendMessageAsync(line.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при попытке осудить.");
            }
        });
    }
}