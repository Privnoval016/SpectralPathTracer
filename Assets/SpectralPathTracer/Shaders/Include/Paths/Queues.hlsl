/**
 * <summary>
 * Counting, appending and dispatch sizes for the live-path queues.
 * </summary>
 */

#ifndef SPT_QUEUES_INCLUDED
#define SPT_QUEUES_INCLUDED

#include "../GpuData.hlsl"

// queue A's count is at byte 0 of the counter buffer, queue B's at byte 4

/**
 * <summary>
 * Reads how many paths a queue holds.
 * </summary>
 * <param name="counters">The counter buffer.</param>
 * <param name="countOffset">The byte offset of the queue's count.</param>
 * <returns>The number of paths in the queue.</returns>
 */
uint QueueCount(ByteAddressBuffer counters, uint countOffset)
{
    return counters.Load(countOffset);
}

/**
 * <summary>
 * Reads how many paths a queue holds, from a counter buffer the kernel also writes.
 * </summary>
 * <param name="counters">The counter buffer.</param>
 * <param name="countOffset">The byte offset of the queue's count.</param>
 * <returns>The number of paths in the queue.</returns>
 */
uint QueueCount(RWByteAddressBuffer counters, uint countOffset)
{
    return counters.Load(countOffset);
}

/**
 * <summary>
 * Appends a path slot to a queue.
 * </summary>
 * <param name="queue">The queue to append to.</param>
 * <param name="counters">The counter buffer.</param>
 * <param name="countOffset">The byte offset of the queue's count.</param>
 * <param name="slot">The path slot to append.</param>
 */
void Enqueue(RWStructuredBuffer<uint> queue, RWByteAddressBuffer counters, uint countOffset, uint slot)
{
    // each thread gets its own slot, so the queue stays packed
    uint queuePosition;
    counters.InterlockedAdd(countOffset, 1u, queuePosition);
    queue[queuePosition] = slot;
}

/**
 * <summary>
 * How many thread groups an indirect dispatch needs to cover a queue.
 * </summary>
 * <param name="count">The number of paths in the queue.</param>
 * <returns>The group count, rounded up.</returns>
 */
uint QueueGroups(uint count)
{
    return (count + QUEUESIZES_GROUP_SIZE - 1u) / QUEUESIZES_GROUP_SIZE;
}

#endif
