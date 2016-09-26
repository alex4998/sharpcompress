using System;
using System.IO;

using SharpCompress.Compressor.Deflate;

namespace SharpCompress.Writer.Zip {
    public struct Crc32 : IEquatable<Crc32>, IComparable<Crc32> {
        private readonly int _Value;

        public Crc32(int iValue) {
            _Value = iValue;
        }

        public Crc32(Stream objStream) {
            if(objStream.CanSeek) {
                long lPosition = objStream.Position;
                _Value = new CRC32().GetCrc32(objStream);
                objStream.Position = lPosition;
            }
            else {
                _Value = new CRC32().GetCrc32(objStream);
            }
        }

        public override int GetHashCode() {
            return _Value.GetHashCode();
        }

        public bool Equals(Crc32 other) {
            return _Value == other._Value;
        }

        public override bool Equals(object obj) {
            return (obj is Crc32) && Equals((Crc32)obj);
        }

        public int CompareTo(Crc32 other) {
            return _Value.CompareTo(other._Value);
        }

        public static implicit operator int(Crc32 sValue) {
            return sValue._Value;
        }
    }
}
