// シード付き乱数(mulberry32。TypeScript 版 src/core/rng.ts と同じ式)。
// 状態は uint 1つなので、ゲーム状態に入れて複製できる。

namespace ShiroariBojo.Core
{
    public sealed class Rng
    {
        public uint State { get; private set; }

        public Rng(uint seed) { State = seed; }

        public static uint SeedFrom(string text)
        {
            uint h = 2166136261;
            foreach (char c in text)
            {
                h ^= c;
                h *= 16777619;
            }
            return h;
        }

        /// <summary>[0,1) の値</summary>
        public double NextDouble()
        {
            uint next = unchecked(State + 0x6D2B79F5u);
            State = next;
            uint t = next;
            t = unchecked((t ^ (t >> 15)) * (t | 1u));
            t ^= unchecked(t + (t ^ (t >> 7)) * (t | 61u));
            return (t ^ (t >> 14)) / 4294967296.0;
        }

        /// <summary>0 以上 maxExclusive 未満の整数</summary>
        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            int v = (int)(NextDouble() * maxExclusive);
            return v >= maxExclusive ? maxExclusive - 1 : v;
        }

        /// <summary>Fisher-Yates</summary>
        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
