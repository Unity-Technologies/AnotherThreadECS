// using Unity.Mathematics;
// using Unity.Entities;
using UnityEngine;

namespace UTJ {

public static class CV
{
    public const float MaxValue = 1e+30f;
    public const float MinValue = -1e+30f;
	public static Vector3[] Vector3ArrayEmpty = new Vector3[0];
	public static Vector2[] Vector2ArrayEmpty = new Vector2[0];
	public static int[] IntArrayEmpty = new int[0];

    public const float MAX_RADIUS = 20f;
    public const float MAX_RADIUS_SQR = MAX_RADIUS*MAX_RADIUS;
    public const float PLAYER_WIDTH_HALF = 0.5f;
    public const float FLOW_VELOCITY = -48f;
}

} // namespace UTJ {
