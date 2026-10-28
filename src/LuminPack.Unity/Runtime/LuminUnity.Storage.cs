using System.Runtime.InteropServices;
using UnityEngine;

namespace LuminPack.Unity
{
    public static unsafe partial class LuminUnity
    {
        /// <summary>Preserves the two sequential vectors without calling a normalizing Ray setter.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RayStorage
        {
            internal Vector3 Origin;
            internal Vector3 Direction;
        }

        /// <summary>Preserves the two sequential vectors without calling a normalizing Ray2D setter.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Ray2DStorage
        {
            internal Vector2 Origin;
            internal Vector2 Direction;
        }
    }
}
