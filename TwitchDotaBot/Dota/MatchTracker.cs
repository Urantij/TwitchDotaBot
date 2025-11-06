using System.Net.Sockets;
using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;

namespace TwitchDotaBot.Dota;

/// <summary>
/// Уведомляет, когда находится новый матч, и позволяет подписаться на обновление о его закрытии.
/// <see cref="MatchContainer.Sub"/>
/// </summary>
public class MatchTracker : IHostedService
{
    private readonly Lock _lock = new();
    private readonly DotaClient _dota;
    private readonly AppConfig _appConfig;
    private readonly ILogger<MatchTracker> _logger;

    public event Action<MatchContainer>? NewMatchFound;

    // мне просто впадлу всё портировать
    public event Action<MatchContainer>? LatestMatchUpdated;

    /// <summary>
    /// Хранит трек последнего известного матча. Даже если неактуален (закрыт)
    /// </summary>
    public MatchContainer? LatestMatch { get; internal set; }

    /// <summary>
    /// Хранит актуальные треки, за которыми нужно следить
    /// </summary>
    private readonly List<MatchContainer> _trackingDataList = [];

    public MatchTracker(IOptions<AppConfig> options, ILogger<MatchTracker> logger, DotaClient dota)
    {
        _appConfig = options.Value;
        _logger = logger;
        _dota = dota;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(() => WorkLoopAsync(cancellationToken: cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Не предполагается использовать напрямую
    /// <see cref="MatchWithTracking.Drop"/> 
    /// </summary>
    public void Drop(MatchContainer data, MatchWithTracking matchWithTracking)
    {
        lock (_lock)
        {
            if (!data.Trackings.Remove(matchWithTracking))
            {
                _logger.LogWarning("Повторная попытка отписаться {id}", matchWithTracking.Model.Id);
                return;
            }

            // Если убирать его из трека, то при создании нового прогноза на уже забытый матч не закроется.
            // if (data.Trackings.Count == 0)
            // {
            //     _trackingDataList.Remove(data);
            // }
        }
    }

    private async Task WorkLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken: cancellationToken);
            }
            catch
            {
                return;
            }

            MatchContainer[] dataListToCheck;
            lock (_lock)
            {
                dataListToCheck = _trackingDataList.ToArray();
            }

            // загрузим самый новый матч и те матчи за которыми следим.
            List<MatchModel> matches = [];
            try
            {
                MatchModel[] loaded = await _dota.LoadMatchesAsync(steamId: _appConfig.SteamId,
                    cancellationToken: cancellationToken);

                MatchModel? latest = loaded.FirstOrDefault();

                if (latest != null)
                {
                    matches.Add(latest);
                }

                // TODO бач каеш было бы заебись
                foreach (MatchContainer toCheck in dataListToCheck)
                {
                    if (toCheck.Model.Id == latest?.Id)
                        continue;

                    loaded = await _dota.LoadMatchesAsync(matchDbId: toCheck.Model.Id,
                        steamId: _appConfig.SteamId,
                        cancellationToken: cancellationToken);

                    if (loaded.Length != 0)
                    {
                        matches.Add(loaded[0]);
                    }
                    else
                    {
                        _logger.LogWarning("Матч пропал в бд {id}", toCheck.Model.Id);
                    }
                }
            }
            catch (System.Net.Http.HttpRequestException e) when (e.InnerException is SocketException
                                                                 {
                                                                     SocketErrorCode: SocketError.ConnectionRefused
                                                                 })
            {
                _logger.LogError("Ошибка при попытке всосать матчи, соединение отказано. Наверное диспенсер здох.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
                }
                catch
                {
                    return;
                }

                continue;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при попытке всосать матчи.");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken: cancellationToken);
                }
                catch
                {
                    return;
                }

                continue;
            }

            if (matches.Count == 0)
                continue;

            using var scope = _lock.EnterScope();

            // ищем объявляем новинку
            {
                // В теории может прийти новейший матч уже закрытый, и с ним тупо ниче не будет. ну и похуй?
                MatchModel freshModel = matches[0];

                if (freshModel.MatchResult == MatchResult.None && LatestMatch?.Model.Id != freshModel.Id)
                {
                    _logger.LogInformation("Открываем матч {id}", freshModel.Id);

                    MatchContainer freshData = new(this, freshModel);
                    LatestMatch = freshData;
                    _trackingDataList.Add(freshData);

                    NewMatchFound?.Invoke(freshData);

                    // TODO а я хочу трекать матчи, на которые никто не подписался?
                    // типа если подпищик будущий появится... хызы короч, не знаю.
                }
            }

            // ищем объявляем закрытки
            foreach (MatchContainer data in dataListToCheck)
            {
                MatchModel? matchNewerModel = matches.FirstOrDefault(m => m.Id == data.Model.Id);

                if (matchNewerModel == null)
                {
                    // TODO по хорошему кидать всем увед, что матч сломан, а затем убирать его из листа
                    // но есть сценарий, когда матчей будет 0, и этот код не сработает.
                    continue;
                }

                data.Model = matchNewerModel;

                if (matchNewerModel.MatchResult == MatchResult.None)
                {
                    if (data == LatestMatch)
                    {
                        LatestMatchUpdated?.Invoke(data);
                    }

                    continue;
                }

                _logger.LogInformation("Закрываем матч {id}", matchNewerModel.Id);

                foreach (MatchWithTracking tracking in data.Trackings)
                {
                    tracking.OnClosed();
                }

                _trackingDataList.Remove(data);
            }
        }
    }
}