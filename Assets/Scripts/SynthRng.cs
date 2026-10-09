namespace Snake3D
{
    /// <summary>Быстрый детерминированный генератор шума (xorshift).</summary>
    public sealed class SynthRng
    {
        private uint _state;

        public SynthRng(uint seed)
        {
            _state = seed == 0 ? 1u : seed;
        }

        public double NextSigned()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state / (double)uint.MaxValue * 2.0 - 1.0;
        }

        public double Next01()
        {
            return NextSigned() * 0.5 + 0.5;
        }
    }
}
