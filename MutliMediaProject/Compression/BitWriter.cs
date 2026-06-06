using System.Collections.Generic;

namespace MutliMediaProject.Compression
{
    /// <summary>
    /// Packs variable-width codes into a byte buffer, MSB-first within each byte.
    /// </summary>
    public sealed class BitWriter
    {
        private readonly List<byte> _buffer = new List<byte>();
        private int _currentByte;
        private int _bitsInCurrent;

        public int ByteCount => _buffer.Count + (_bitsInCurrent > 0 ? 1 : 0);

        public void WriteBits(uint value, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
            {
                int bit = (int)((value >> i) & 1);
                _currentByte = (_currentByte << 1) | bit;
                _bitsInCurrent++;
                if (_bitsInCurrent == 8)
                {
                    _buffer.Add((byte)_currentByte);
                    _currentByte = 0;
                    _bitsInCurrent = 0;
                }
            }
        }

        public byte[] ToArray()
        {
            if (_bitsInCurrent > 0)
            {
                int aligned = _currentByte << (8 - _bitsInCurrent);
                _buffer.Add((byte)aligned);
                _currentByte = 0;
                _bitsInCurrent = 0;
            }
            return _buffer.ToArray();
        }
    }
}
