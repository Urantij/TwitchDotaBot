using Dota2Dispenser.Shared.Models;

namespace TwitchDotaBot.Dota;

/// <summary>
/// Контейнер определённого матча. Позволяет подписаться на события.
/// <see cref="Sub"/>
/// </summary>
public class MatchContainer
{
    // )
    internal MatchTracker Tracker { get; }

    // )
    internal List<MatchWithTracking> Trackings { get; } = [];

    /// <summary>
    /// Часто обновляется
    /// </summary>
    public MatchModel Model { get; internal set; }

    public MatchContainer(MatchTracker tracker, MatchModel model)
    {
        Tracker = tracker;
        Model = model;
    }

    public MatchWithTracking Sub()
    {
        MatchWithTracking matchWithTracking = new(this);

        Trackings.Add(matchWithTracking);

        return matchWithTracking;
    }
}