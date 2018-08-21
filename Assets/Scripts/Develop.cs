using Unity.Burst;
using UnityEngine;

namespace UTJ {

public static class Develop {
    [BurstDiscard] public static void print<T>(T v) { Debug.Log(v); }
}

} // namespace UTJ {
