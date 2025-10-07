using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class PrevPlayersCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var dota = provider.GetRequiredService<DotaClient>();
        var heroes = provider.GetRequiredService<DotaHeroes>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();
        var chatBot = provider.GetRequiredService<ChatBot>();

        if (!heroes.HasAny())
        {
            await chatBot.Channel.SendMessageAsync("В программе нет героев...", e.id);
            return;
        }

        MatchModel[] matches = await dota.LoadMatchesAsync(steamId: appOptions.Value.SteamId, limit: 5,
            cancellationToken: cancellationToken);

        MatchModel? recentOver = matches.FirstOrDefault(m =>
            m.MatchResult != MatchResult.None && m.MatchResult != MatchResult.EarlyLeave);

        MatchModel? latest = matches.FirstOrDefault();

        // третья проверка не нужна, но иде ноет
        if (recentOver == null || recentOver == latest || latest == null)
        {
            await chatBot.Channel.SendMessageAsync("Не нашёл, увы.", e.id);
            return;
        }

        if (recentOver.MatchResult == MatchResult.Finished)
        {
            if (recentOver.Players?.All(p => p.HeroId != 0) != true)
            {
                await chatBot.Channel.SendMessageAsync("Нет героев в прошлом матче, увы.", e.id);
                return;
            }

            if (latest.Players?.All(p => p.HeroId != 0) != true)
            {
                await chatBot.Channel.SendMessageAsync("Пока нет всех героев, увы.", e.id);
                return;
            }

            var prevs = latest.Players.Select(latestPlayer =>
                {
                    PlayerModel? recentPlayer =
                        recentOver.Players.FirstOrDefault(p => p.SteamId == latestPlayer.SteamId);

                    return new
                    {
                        latestPlayer,
                        recentPlayer
                    };
                })
                .Where(p => p.recentPlayer != null)
                .Where(p => p.latestPlayer.SteamId != appOptions.Value.SteamId)
                .Select(p => new
                {
                    now = heroes.FindHero(p.latestPlayer.HeroId),
                    was = heroes.FindHero(p.recentPlayer.HeroId)
                })
                .Select(pair => $"{pair.was?.Name ?? "???"} => {pair.now?.Name ?? "???"}")
                .ToArray();

            if (prevs.Length == 0)
            {
                await chatBot.Channel.SendMessageAsync("Никого нет.", e.id);
                return;
            }

            string reply = string.Join(", ", prevs);
            
            await chatBot.Channel.SendMessageAsync(reply, e.id);
            return;
        }

        await chatBot.Channel.SendMessageAsync("Непонятно.", e.id);
    }
}