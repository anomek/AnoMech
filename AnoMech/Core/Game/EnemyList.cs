using System.Collections.Generic;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game;

// Plays the server's side of the enemy list: a Hater packet about once a second with every
// listed enemy, so an enemy joins or leaves the list up to a second after it qualifies, as in
// retail. The game resolves each name when the packet lands, so the resend also fills in an
// enemy whose actor arrived after the previous one.
public sealed class EnemyList
{
    private const float SendInterval = 1f;

    // The sim has no enmity table; every listed enemy shows the player at full enmity.
    private const byte PlayerEnmity = 100;

    private readonly List<Hater> haters = new();
    private float untilNextSend;
    private bool sentAny;

    public void Tick(IEnumerable<SimEnemy> enemies, float deltaSeconds)
    {
        untilNextSend -= deltaSeconds;
        if (untilNextSend > 0f) return;
        untilNextSend = SendInterval;

        haters.Clear();
        foreach (var enemy in enemies)
            if (enemy.IsActive && enemy.InEnemyList && enemy.EntityId != 0)
                haters.Add(new Hater(enemy.EntityId, PlayerEnmity));

        // Nobody else sends one in the inn, so an empty list only needs sending once.
        if (haters.Count == 0 && !sentAny) return;
        Natives.HaterList.Receive(haters);
        sentAny = haters.Count > 0;
    }

    public void Clear()
    {
        if (sentAny) Natives.HaterList.Receive([]);
        haters.Clear();
        untilNextSend = 0f;
        sentAny = false;
    }
}
