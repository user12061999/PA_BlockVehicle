namespace Gre.pjcode.Scenes.InGame
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public enum MinoShapeType
    {
        A = 0,
        B,
        C1,
        C2,
        D1,
        D2,
        E,
        F,
        G,
        H1,
        H2,
        I,
        J,
        K,
        L,
        M,
        N1,
        N2,
        Max,
    }

    public static class MinoShapeTypeExtensions
    {
        static readonly Dictionary<MinoShapeType, Vector2Int[]> Patterns = new Dictionary<MinoShapeType, Vector2Int[]>()
        {
            { MinoShapeType.A, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) } },
            { MinoShapeType.B, new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) } },
            { MinoShapeType.C1, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) } },
            { MinoShapeType.C2, new[] { Vector2Int.zero, new Vector2Int(-1, 1), new Vector2Int(0, 1), new Vector2Int(1, 0) } },
            { MinoShapeType.D1, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) } },
            { MinoShapeType.D2, new[] { Vector2Int.zero, new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(1, 0) } },
            { MinoShapeType.E, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) } },
            { MinoShapeType.F, new[] { Vector2Int.zero } },
            { MinoShapeType.G, new[] { Vector2Int.zero, new Vector2Int(0, 1) } },
            { MinoShapeType.H1, new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(0, -1) } },
            { MinoShapeType.H2, new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 0) } },
            { MinoShapeType.I, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) } },
            { MinoShapeType.J, new[] { Vector2Int.zero, new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, -1), new Vector2Int(1, -1) } },
            { MinoShapeType.K, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(1, 1) } },
            { MinoShapeType.L, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(-2, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) } },
            { MinoShapeType.M, new[] { Vector2Int.zero, new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(0, -1), new Vector2Int(1, -1) } },
            { MinoShapeType.N1, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) } },
            { MinoShapeType.N2, new[] { Vector2Int.zero, new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1) } },
        };

        public static Vector2Int[] GetBlockPattern(this MinoShapeType type, int rotate)
        {
            if (!Patterns.TryGetValue(type, out Vector2Int[] src)) return new Vector2Int[0];

            Vector2Int[] result = new Vector2Int[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Vector2Int p = src[i];
                for (int r = 0; r < rotate % 4; r++) p = new Vector2Int(-p.y, p.x);
                result[i] = p;
            }

            return result;
        }
    }
}
