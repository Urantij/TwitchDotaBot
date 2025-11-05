using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch.Commands;

public class WinrateAgainstCommand : BaseCommand
{
    public override async Task DoAsync(IServiceProvider provider, string[] args, TwitchPrivateMessage e,
        CancellationToken cancellationToken = default)
    {
        var dota = provider.GetRequiredService<DotaClient>();
        var chatbot = provider.GetRequiredService<ChatBot>();
        var heroes = provider.GetRequiredService<DotaHeroes>();
        var appOptions = provider.GetRequiredService<IOptions<AppConfig>>();

        MatchModel? match = dota.CurrentMatch;

        if (match == null)
        {
            await chatbot.Channel.SendMessageAsync("Не вижу игру.", e.id);
            return;
        }

        if (match.Players == null)
        {
            await chatbot.Channel.SendMessageAsync("Матч есть, но герои ещё неизвестны.", e.id);
            return;
        }

        if (args.Length == 0)
        {
            await chatbot.Channel.SendMessageAsync("=команда имя героя", e.id);
            return;
        }

        string heroName = string.Join(" ", args);

        HeroModel? hero = heroes.TryFindHero(heroName);

        // TODO было бы здорово по нику проверять
        if (hero == null)
        {
            await chatbot.Channel.SendMessageAsync("Такого не знаю.", e.id);
            return;
        }

        PlayerModel? player = match.Players.FirstOrDefault(p => p.HeroId == hero.Id);

        if (player == null)
        {
            await chatbot.Channel.SendMessageAsync($"Не мог найти героя {hero.LocalizedName} в матче.", e.id);
            return;
        }

        List<MatchModel> matches;
        try
        {
            matches = await dota.LoadAllPlayersMatchesAsync(player.SteamId);
        }
        catch
        {
            await chatbot.Channel.SendMessageAsync($"Внутренняя проблема в приложении.", e.id);
            return;
        }

        ulong streamerId = appOptions.Value.SteamId;
        ulong playerId = player.SteamId;

        // нужно найти матчи, в которых стример и указанный игрок в разных командах

        int wins = 0;
        int loses = 0;
        foreach (MatchModel m in matches.Where(m => m.MatchResult == MatchResult.Finished))
        {
            PlayerModel? streamer = m.Players.FirstOrDefault(p => p.SteamId == streamerId);

            if (streamer == null)
                continue;

            PlayerModel? p = m.Players.FirstOrDefault(p => p.SteamId == playerId);

            if (p == null)
                continue;

            if (p.TeamNumber == streamer.TeamNumber)
                continue;

            bool? win = Worker.DetermineWin(m, streamerId);

            if (win == true)
            {
                wins++;
            }
            else if (win == false)
            {
                loses++;
            }
        }

        if (wins == 0 && loses == 0)
        {
            await chatbot.Channel.SendMessageAsync($"Противостояния с {hero.LocalizedName} не найдены.", e.id);
            return;
        }

        int total = wins + loses;

        float winrate = ((float)wins / (float)total) * 100;

        await chatbot.Channel.SendMessageAsync($"Против {hero.LocalizedName} {winrate:F2}% ({wins}-{loses})", e.id);
    }
}