#ifndef SPT_TEST_SCENE_INCLUDED
#define SPT_TEST_SCENE_INCLUDED

#include "Quadric.hlsl"

// temporary test sphere
static const float3 SPHERE_CENTER = float3(0.0f, 1.0f, 0.0f);
static const float SPHERE_RADIUS = 1.0f;

/**
 * <summary>
 * The quadric matrix of the temporary test sphere, shared by every kernel that needs the scene.
 * </summary>
 * <returns>A 4x4 matrix representing the test sphere.</returns>
 */
float4x4 TestSphereQuadric()
{
    return SphereQuadric(SPHERE_CENTER, SPHERE_RADIUS);
}

#endif