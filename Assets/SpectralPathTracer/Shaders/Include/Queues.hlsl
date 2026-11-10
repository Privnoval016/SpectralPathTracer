#ifndef SPT_QUEUES_INCLUDED
#define SPT_QUEUES_INCLUDED

// queue A's count is at byte 0 of the counter buffer, queue B's at byte 4
static const uint QUEUE_GROUP_SIZE = 64u; // matches [numthreads(64, 1, 1)] in Intersect and Shade

/**
 * <summary>
 * How many thread groups an indirect dispatch needs to cover a queue.
 * </summary>
 * <param name="count">The number of paths in the queue.</param>
 * <returns>The group count, rounded up.</returns>
 */
uint QueueGroups(uint count)
{
    return (count + QUEUE_GROUP_SIZE - 1u) / QUEUE_GROUP_SIZE;
}

#endif