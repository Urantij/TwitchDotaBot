using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Json;
using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;

namespace TwitchDotaBot.Dota;

public class DotaConfig
{
    public Uri ServerAddress { get; set; }
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
        return WorkLoopAsync(cancellationToken);
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
                models = await LoadMatchesAsync(matchDbId, _appConfig.SteamId, cancellationToken);
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

    private async Task<MatchModel[]> LoadMatchesAsync(int? matchDbId, ulong? steamId,
        CancellationToken cancellationToken)
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

        UriBuilder uriBuilder = new(new Uri(_dotaConfig.ServerAddress, Dota2DispenserPoints.matches));
        uriBuilder.Query = query.ToString();

        using HttpRequestMessage message = new(HttpMethod.Get, uriBuilder.Uri);

        using var response =
            await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        MatchModel[]? matches =
            await response.Content.ReadFromJsonAsync<MatchModel[]>(cancellationToken: cancellationToken);

        if (matches == null)
        {
            throw new NullReferenceException($"{nameof(matches)} is null");
        }

        return matches;
    }
}