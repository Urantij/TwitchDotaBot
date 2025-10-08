using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch;

public abstract class BaseCommand
{
    public required string[] Triggers { get; init; }
    public string[] LiteralTriggers { get; init; } = [];

    public required TimeSpan Cooldown { get; init; }

    /// <summary>
    /// Только модеры могут юзать. По умолчанию тру.
    /// </summary>
    public bool ModsOnly { get; init; } = true;

    /// <summary>
    /// Тока я могу юзать. Фолс по умолчанию.
    /// </summary>
    public bool MainVillainOnly { get; init; } = false;

    public DateTimeOffset? LastUse { get; set; } = null;

    public abstract Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default);

    // Кто-то скажет, нужно делать хелпер. Я скажу, что никто не скажет, я один тут.
    public static PlayerModel? GetPlayer(MatchModel match, ulong id)
    {
        return match.Players?.FirstOrDefault(p => p.SteamId == id);
    }

    public static bool? IsWinner(MatchModel match, ulong id)
    {
        if (match.MatchResult != MatchResult.Finished || match.DetailsInfo?.RadiantWin == null)
            return null;

        PlayerModel? target = GetPlayer(match, id);
        if (target?.TeamNumber == null)
            return null;

        bool radiant = target.TeamNumber == 0;

        return match.DetailsInfo.RadiantWin == radiant;
    }

    public static string GetTimeString(TimeSpan time)
    {
        if (time.TotalSeconds <= 60)
            return $"{time.TotalSeconds:F0} секунд";

        if (time.TotalMinutes <= 60)
            return $"{time.TotalMinutes:F0} минут";

        return $"{time.TotalHours:F0} часов";
    }
}