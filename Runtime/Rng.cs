using System;

namespace Parlance
{
    public static class Rng
    {
        /// <summary>
        /// Returns a delegate producing the same float sequence as the reference
        /// implementation for the same seed. Values are in [0, 1).
        /// </summary>
        public static Func<float> Stream(uint seedValue)
        {
            uint state = seedValue;
            return () =>
            {
                unchecked
                {
                    state += 0x6d2b79f5;
                    uint z = state;
                    z = (z ^ (z >> 15)) * (z | 1);
                    z ^= z + (z ^ (z >> 7)) * (z | 61);
                    return (float)(z ^ (z >> 14)) / 4294967296.0f;
                }
            };
        }

        /// <summary>
        /// The RNG for step `stepIndex` of a play session: a pure function of the seed
        /// and the step, never a running stream. Rewinding to a step and replaying it
        /// must produce the same roll, which a stateful generator could not promise.
        /// </summary>
        public static Func<float> ForStep(uint seedValue, uint stepIndex)
        {
            unchecked
            {
                return Stream(seedValue + stepIndex);
            }
        }
    }
}
