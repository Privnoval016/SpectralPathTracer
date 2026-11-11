/**
 * <summary>
 * Ends dim paths at random and boosts the survivors, so the average stays exact.
 * </summary>
 */

#ifndef SPT_RUSSIAN_ROULETTE_INCLUDED
#define SPT_RUSSIAN_ROULETTE_INCLUDED

#include "../Lanes.hlsl"

/**
 * <summary>
 * Plays Russian roulette, surviving with probability equal to the largest lane.
 * </summary>
 * <param name="throughput">The path's throughput, boosted in place when the roll is played.</param>
 * <param name="u">A uniform random number in [0, 1).</param>
 * <returns>True if the path survives.</returns>
 */
bool SurvivesRoulette(inout Lanes throughput, float u)
{
    bool survives = true;
    float largestLane = MaxLane(throughput);

    // paths at full strength or brighter always continue
    if (largestLane < 1.0f)
    {
        survives = u < largestLane;
        throughput /= largestLane; // throughput scaling to be unbiased
    }

    return survives;
}

#endif
