using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;

namespace TwitchDotaBot.Dota;

/// <summary>
/// Подписка на инфу о матче.
/// </summary>
public class MatchWithTracking
{
    private readonly MatchContainer _data;

    // )
    public MatchModel Model => _data.Model;

    /// <summary>
    /// <see cref="MatchModel.MatchResult"/> Перестал быть <see cref="MatchResult.None"/>
    /// </summary>
    public Action? Closed { get; set; }

    public MatchWithTracking(MatchContainer data)
    {
        _data = data;
    }

    public void Drop()
    {
        _data.Tracker.Drop(_data, this);
    }

    // )
    internal void OnClosed()
    {
        Closed?.Invoke();
    }
}