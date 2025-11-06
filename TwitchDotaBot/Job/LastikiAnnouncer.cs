using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;
using TwitchDotaBot.Twitch.Commands;

namespace TwitchDotaBot.Job;

public class LastikiAnnouncer : IHostedService
{
    private readonly MatchTracker _tracker;
    private readonly ChatBot _chat;
    private readonly DotaHeroes _heroes;
    private readonly AppConfig _options;
    private readonly ILogger<LastikiAnnouncer> _logger;

    private MatchModel? _lastAnnounce;

    public LastikiAnnouncer(MatchTracker tracker, ChatBot chat, DotaHeroes heroes, IOptions<AppConfig> options,
        ILogger<LastikiAnnouncer> logger)
    {
        _tracker = tracker;
        _chat = chat;
        _heroes = heroes;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _tracker.LatestMatchUpdated += MatchUpdated;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _tracker.LatestMatchUpdated -= MatchUpdated;

        return Task.CompletedTask;
    }

    private void MatchUpdated(MatchContainer container)
    {
        if (!_options.AnnounceLastiki)
            return;

        if (container.Model.Id == _lastAnnounce?.Id)
            return;

        if (container.Model.Players == null)
            return;

        if (container.Model.Players.Any(p => p.HeroId == 0))
            return;

        if (_lastAnnounce == null)
        {
            _lastAnnounce = container.Model;
            return;
        }

        string[] prevs =
            PrevPlayersCommand.GenerateArrows(container.Model.Players, _lastAnnounce.Players, _options.SteamId,
                _heroes);

        _lastAnnounce = container.Model;

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