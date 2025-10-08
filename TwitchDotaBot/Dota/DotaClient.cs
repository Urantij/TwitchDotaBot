using System.Collections.Specialized;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;

namespace TwitchDotaBot.Dota;

[JsonSerializable(typeof(MatchModel))]
[JsonSerializable(typeof(MatchModel[]))]
internal partial class DotaSerializeContext : JsonSerializerContext
{
}

public class DotaConfig
{
    public required Uri ServerAddress { get; init; }
}

public class DotaClient : IHostedService
{
    private readonly HttpClient _client;
    private readonly DotaConfig _dotaConfig;
    private readonly AppConfig _appConfig;
    private readonly ILogger<DotaClient> _logger;

    /// <summary>
    /// Объект меняется
    /// </summary>
    public MatchModel? CurrentMatch { get; private set; }

    public event Action<MatchModel>? NewMatchFound;
    public event Action<MatchModel>? MatchUpdated;
    public event Action<MatchModel>? MatchClosed;

    public DotaClient(IOptions<DotaConfig> dotaOptions, IOptions<AppConfig> appOptions,
        ILogger<DotaClient> logger)
    {
        _client = new HttpClient();

        _dotaConfig = dotaOptions.Value;
        _appConfig = appOptions.Value;
        _logger = logger;
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

            int? matchDbId = null;
            if (CurrentMatch is { MatchResult: MatchResult.None })
            {
                matchDbId = CurrentMatch.Id;
            }

            MatchModel[] models;
            try
            {
                models = await LoadMatchesAsync(matchDbId: matchDbId, steamId: _appConfig.SteamId,
                    cancellationToken: cancellationToken);
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

            if (models.Length == 0)
                continue;

            MatchModel freshModel = models[0];

            if (matchDbId != null)
            {
                CurrentMatch = freshModel;

                if (CurrentMatch.MatchResult != MatchResult.None)
                {
                    _logger.LogInformation("Закрываем матч {id}", CurrentMatch.Id);
                    MatchClosed?.Invoke(CurrentMatch);
                }
                else
                {
                    MatchUpdated?.Invoke(CurrentMatch);
                }

                continue;
            }

            if (CurrentMatch?.Id != freshModel.Id)
            {
                if (freshModel.MatchResult != MatchResult.None)
                    continue;

                _logger.LogInformation("Открываем матч {id}", freshModel.Id);

                CurrentMatch = freshModel;
                NewMatchFound?.Invoke(CurrentMatch);
                continue;
            }

            CurrentMatch = freshModel;
        }
    }

    public async Task<MatchModel[]> LoadMatchesAsync(int? matchDbId = null, ulong? steamId = null,
        DateTimeOffset? afterDate = null, int? limit = null,
        CancellationToken cancellationToken = default)
    {
        NameValueCollection query = System.Web.HttpUtility.ParseQueryString(string.Empty);

        if (matchDbId != null)
        {
            query[Dota2DispenserParams.dispenserMatchIdFilter] = matchDbId.Value.ToString();
        }

        if (steamId != null)
        {
            query[Dota2DispenserParams.steamIdFilter] = steamId.Value.ToString();
        }

        if (afterDate != null)
        {
            query[Dota2DispenserParams.afterDateTimeFilter] = afterDate.Value.ToUnixTimeSeconds().ToString();
        }

        if (limit != null)
        {
            query[Dota2DispenserParams.limitFilter] = limit.Value.ToString();
        }

        UriBuilder uriBuilder = new(new Uri(_dotaConfig.ServerAddress, Dota2DispenserPoints.matches));
        uriBuilder.Query = query.ToString();

        using HttpRequestMessage message = new(HttpMethod.Get, uriBuilder.Uri);

        using var response =
            await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (JsonSerializer.Deserialize(content, typeof(MatchModel[]), DotaSerializeContext.Default) is not MatchModel[]
            matches)
        {
            throw new NullReferenceException($"{nameof(matches)} is null");
        }

        return matches;
    }
}