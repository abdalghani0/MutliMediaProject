namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Reads variable-width codes from a byte buffer in the same MSB-first order
    /// used by <see cref="BitWriter"/>. Past the end of the stream the reader
    /// returns zero so callers can pad cleanly.
    /// </summary>
    public sealed class BitReader
    {
        private readonly byte[] _data;
        private int _bytePos;
        private int _bitPos;

        public BitReader(byte[] data) { _data = data ?? new byte[0]; }

        public uint ReadBits(int bitCount)
        {
            uint value = 0;
            for (int i = 0; i < bitCount; i++)
            {
                if (_bytePos >= _data.Length)
                {
                    value <<= 1;
                    continue;
                }
                int bit = (_data[_bytePos] >> (7 - _bitPos)) & 1;
                value = (value << 1) | (uint)bit;
                _bitPos++;
                if (_bitPos == 8) { _bitPos = 0; _bytePos++; }
            }
            return value;
        }
    }
}
