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

/// <summary>
/// Позволяет делать запросы в диспенсер.
/// </summary>
public class DotaClient
{
    private readonly HttpClient _client;
    private readonly DotaConfig _dotaConfig;
    private readonly ILogger<DotaClient> _logger;

    public DotaClient(IOptions<DotaConfig> dotaOptions, ILogger<DotaClient> logger)
    {
        _client = new HttpClient();

        _dotaConfig = dotaOptions.Value;
        _logger = logger;
    }

    // в теории этот метод должен лежать в хелпере, но мне похуй
    /// <summary>
    /// не сортированы по айди
    /// </summary>
    /// <param name="playerSteamId"></param>
    /// <returns></returns>
    public async Task<List<MatchModel>> LoadAllPlayersMatchesAsync(ulong playerSteamId)
    {
        // TODO в теории нужно грузить только матчи, где И стример И цель. Но. мне. впадлу.

        List<MatchModel> result = [];

        int? beforeId = null;

        while (true)
        {
            MatchModel[] batch = await LoadMatchesAsync(steamId: playerSteamId, beforeId: beforeId);

            if (batch.Length > 0)
            {
                beforeId = batch.Last().Id;

                result.AddRange(batch);
            }
            else
            {
                break;
            }
        }

        return result;
    }

    public async Task<MatchModel[]> LoadMatchesAsync(int? matchDbId = null, ulong? steamId = null,
        DateTimeOffset? afterDate = null, int? limit = null, int? sinceId = null, int? beforeId = null,
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

        if (sinceId != null)
        {
            query[Dota2DispenserParams.sinceIdFilter] = sinceId.Value.ToString();
        }

        if (beforeId != null)
        {
            query[Dota2DispenserParams.beforeIdFilter] = beforeId.Value.ToString();
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