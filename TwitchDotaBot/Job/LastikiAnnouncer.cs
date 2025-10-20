using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;
using TwitchDotaBot.Twitch.Commands;

namespace TwitchDotaBot.Job;

public class LastikiAnnouncer : IHostedService
{
    private readonly DotaClient _dota;
    private readonly ChatBot _chat;
    private readonly DotaHeroes _heroes;
    private readonly AppConfig _options;
    private readonly ILogger<LastikiAnnouncer> _logger;

    private MatchModel? _lastAnnounce;

    public LastikiAnnouncer(DotaClient dota, ChatBot chat, DotaHeroes heroes, IOptions<AppConfig> options,
        ILogger<LastikiAnnouncer> logger)
    {
        _dota = dota;
        _chat = chat;
        _heroes = heroes;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
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
        if (!_options.AnnounceLastiki)
            return;

        if (obj.Id == _lastAnnounce?.Id)
            return;

        if (obj.Players == null)
            return;

        if (obj.Players.Any(p => p.HeroId == 0))
            return;

        if (_lastAnnounce == null)
        {
            _lastAnnounce = obj;
            return;
        }

        string[] prevs =
            PrevPlayersCommand.GenerateArrows(obj.Players, _lastAnnounce.Players, _options.SteamId, _heroes);

        _lastAnnounce = obj;

        _logger.LogInformation("Нашли всех игроков, стрелочек: {arrows}", prevs.Length);

        if (prevs.Length == 0)
            return;

        string message = "Найдены все игроки. " + string.Join(", ", prevs);

        Task.Run(async () =>
        {
            try
            {
                await _chat.Channel.SendMessageAsync(message);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Не удалось отправить сообщение в чат.");
            }
        });
    }
}