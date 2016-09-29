using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressor;
using SharpCompress.Compressor.BZip2;
using SharpCompress.Compressor.Deflate;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Compressor.PPMd;
using SharpCompress.Converter;
using SharpCompress.IO;
using DeflateStream = SharpCompress.Compressor.Deflate.DeflateStream;

namespace SharpCompress.Writer.Zip
{
    public class ZipWriter : AbstractWriter
    {
        private readonly ZipCompressionInfo zipCompressionInfo;
        private readonly string password;
        private readonly PpmdProperties ppmdProperties = new PpmdProperties(); // Caching properties to speed up PPMd
        private readonly List<ZipCentralDirectoryEntry> entries = new List<ZipCentralDirectoryEntry>();
        private readonly string zipComment;
        private long streamPosition;

        internal ZipWriter(Stream destination, CompressionInfo compressionInfo, string zipComment, string password = null, bool leaveOpen = false)
            : base(ArchiveType.Zip)
        {
            this.zipComment = zipComment ?? string.Empty;

            this.zipCompressionInfo = new ZipCompressionInfo(compressionInfo);
            this.password = password;

            InitalizeStream(destination, !leaveOpen);
        }

        public static ZipWriter Open(Stream stream, CompressionInfo compressionInfo, string zipComment, string password = null, bool leaveOpen = false) {
            stream.CheckNotNull("stream");
            return new ZipWriter(stream, compressionInfo, zipComment, password, leaveOpen);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                uint size = 0;
                foreach (ZipCentralDirectoryEntry entry in entries)
                {
                    size += entry.Write(OutputStream, zipCompressionInfo.Compression);
                }
                WriteEndRecord(size);
            }
            base.Dispose(isDisposing);
        }

        public override void Write(string entryPath, Stream source, DateTime? modificationTime)
        {
            // UNDONE: This method will fail if the source stream is not seekable because Crc32 will iterate through the stream and won't return the position back
            Write(entryPath, source, modificationTime, null, null, String.IsNullOrEmpty(password) ? (Crc32?)null : new Crc32(source));
        }

        public void WriteDirectoryEntry(string entryPath, DateTime? modificationTime, string comment) {
            entryPath = entryPath.TrimEnd('\\');
            if(!entryPath.EndsWith("/")) {
                entryPath += '/';
            }
            using(WriteToStream(entryPath, modificationTime, comment, null, new Crc32(0))) {
            }
        }

        public void Write(string entryPath, Stream source, DateTime? modificationTime, string comment, string password = null, Crc32? crc = null, CompressionInfo compressionInfo = null)
        {
            using (Stream output = WriteToStream(entryPath, modificationTime, comment, password, crc, compressionInfo))
            {
                source.TransferTo(output);
            }
        }

        public Stream WriteToStream(string entryPath, DateTime? modificationTime, string comment, string password = null, Crc32? crc = null, CompressionInfo compressionInfo = null)
        {
            entryPath = NormalizeFilename(entryPath);
            modificationTime = modificationTime ?? DateTime.Now;
            comment = comment ?? string.Empty;

            var explicitPassword = password ?? this.password;
            bool doEncryption = !String.IsNullOrEmpty(explicitPassword);

            var entry = new ZipCentralDirectoryEntry() {
                Comment = comment,
                FileName = entryPath,
                ModificationTime = modificationTime,
                HeaderOffset = (uint) streamPosition,
                IsEncrypted = doEncryption
            };

            var headersize = (uint)WriteHeader(entryPath, modificationTime, doEncryption, compressionInfo);

            Stream outputStream;
            if(doEncryption) {
                byte[] encryptionHeader;
                PkwareTraditionalEncryptionData pkwareTraditionalEncryptionData;
                if(OutputStream.CanSeek) {
                    if(crc.HasValue) {
                        pkwareTraditionalEncryptionData = PkwareTraditionalEncryptionData.ForWrite(explicitPassword, (uint)(int)crc, out encryptionHeader);
                    }
                    else {
                        throw new ArgumentNullException(nameof(crc), "Crc32 must be provided when password is set and target archive stream supports seeking");
                    }
                }
                else {
                    pkwareTraditionalEncryptionData = PkwareTraditionalEncryptionData.ForWrite(explicitPassword, modificationTime, out encryptionHeader);
                }
                OutputStream.Write(encryptionHeader, 0, encryptionHeader.Length);
                entry.Compressed += (uint)encryptionHeader.Length;
                outputStream = new PkwareTraditionalCryptoStream(OutputStream, pkwareTraditionalEncryptionData, CryptoMode.Encrypt);
            }
            else {
                outputStream = OutputStream;
            }

            streamPosition += headersize;
            return new ZipWritingStream(this, outputStream, entry, compressionInfo);
        }

        private string NormalizeFilename(string filename)
        {
            filename = filename.Replace('\\', '/');

            int pos = filename.IndexOf(':');
            if (pos >= 0)
                filename = filename.Remove(0, pos + 1);

            return filename.TrimStart('/');
        }

        private int WriteHeader(string filename, DateTime? modificationTime, bool doEncryption, CompressionInfo compressionInfo)
        {
            var explicitZipCompressionInfo = compressionInfo != null ? new ZipCompressionInfo(compressionInfo) : this.zipCompressionInfo;

            byte[] encodedFilename = ArchiveEncoding.Default.GetBytes(filename);

            OutputStream.Write(DataConverter.LittleEndian.GetBytes(ZipHeaderFactory.ENTRY_HEADER_BYTES), 0, 4);
		    if (explicitZipCompressionInfo.Compression == ZipCompressionMethod.Deflate)
		    {
			    OutputStream.Write(new byte[] {20, 0}, 0, 2); //older version which is more compatible 
		    }
		    else
		    {
			    OutputStream.Write(new byte[] {63, 0}, 0, 2); //version says we used PPMd or LZMA
		    }            
            HeaderFlags flags = (ArchiveEncoding.Default == Encoding.UTF8) ? HeaderFlags.UTF8 : HeaderFlags.Undefined;
            if(doEncryption) {
                flags |= HeaderFlags.Encrypted;
            }
            if (!OutputStream.CanSeek)
            {
                flags |= HeaderFlags.UsePostDataDescriptor;
                if (explicitZipCompressionInfo.Compression == ZipCompressionMethod.LZMA)
                {
                    flags |= HeaderFlags.Bit1; // eos marker
                }
            }
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) flags), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)explicitZipCompressionInfo.Compression), 0, 2); // zipping method
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(modificationTime.DateTimeToDosTime()), 0, 4);
            // zipping date and time
            OutputStream.Write(new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0}, 0, 12);
            // unused CRC, un/compressed size, updated later
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) encodedFilename.Length), 0, 2); // filename length
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) 0), 0, 2); // extra length
            OutputStream.Write(encodedFilename, 0, encodedFilename.Length);

            return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length;
        }

        private void WriteFooter(uint crc, uint compressed, uint uncompressed)
        {
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(crc), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(compressed), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(uncompressed), 0, 4);
        }

        private void WriteEndRecord(uint size)
        {
            byte[] encodedComment = ArchiveEncoding.Default.GetBytes(zipComment);

            OutputStream.Write(new byte[] {80, 75, 5, 6, 0, 0, 0, 0}, 0, 8);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) entries.Count), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) entries.Count), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(size), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint) streamPosition), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort) encodedComment.Length), 0, 2);
            OutputStream.Write(encodedComment, 0, encodedComment.Length);
        }

        #region Nested type: ZipWritingStream

        private class ZipWritingStream : Stream
        {
            private readonly ZipWriter writer;

            private readonly CRC32 crc = new CRC32();
            private readonly ZipCentralDirectoryEntry entry;
            private readonly ZipCompressionInfo compressionInfo;
            private uint decompressed;

            private readonly Stream writeStream;
            private CountingWritableStream counting;

            public ZipWritingStream(ZipWriter writer, Stream originalStream, ZipCentralDirectoryEntry entry, CompressionInfo compressionInfo)
            {
                this.writer = writer;

                this.entry = entry;
				this.compressionInfo = compressionInfo == null ? writer.zipCompressionInfo : new ZipCompressionInfo(compressionInfo);
                writeStream = GetWriteStream(originalStream);
            }

            public override bool CanRead
            {
                get { return false; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return true; }
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            private Stream GetWriteStream(Stream originalStream)
            {
                counting = new CountingWritableStream(originalStream);

                switch (compressionInfo.Compression)
                {
                    case ZipCompressionMethod.None:
                        return counting;
                    case ZipCompressionMethod.Deflate:
                        return new DeflateStream(counting, CompressionMode.Compress, compressionInfo.DeflateCompressionLevel, true);
                    case ZipCompressionMethod.BZip2:
                        return new BZip2Stream(counting, CompressionMode.Compress, true);
                    case ZipCompressionMethod.LZMA:
                        counting.WriteByte(9);
                        counting.WriteByte(20);
                        counting.WriteByte(5);
                        counting.WriteByte(0);

                        LzmaStream lzmaStream = new LzmaStream(new LzmaEncoderProperties(!originalStream.CanSeek), false, counting);
                        counting.Write(lzmaStream.Properties, 0, lzmaStream.Properties.Length);
                        return lzmaStream;
                    case ZipCompressionMethod.PPMd:
                        counting.Write(writer.ppmdProperties.Properties, 0, 2);
                        return new PpmdStream(writer.ppmdProperties, counting, true);
                    default:
                        throw new NotSupportedException("CompressionMethod: " + compressionInfo.Compression);
                }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    writeStream.Dispose();
                    entry.Crc = (uint) crc.Crc32Result;
                    entry.Compressed += counting.Count;
                    entry.Decompressed = decompressed;
                    if (writer.OutputStream.CanSeek)
                    {
                        writer.OutputStream.Position = entry.HeaderOffset + 14;
                        writer.WriteFooter(entry.Crc, entry.Compressed, decompressed);
                        writer.OutputStream.Position = writer.streamPosition + entry.Compressed;
                        writer.streamPosition += entry.Compressed;
                    }
                    else
                    {
                        writer.OutputStream.Write(DataConverter.LittleEndian.GetBytes(ZipHeaderFactory.POST_DATA_DESCRIPTOR), 0, 4);
                        writer.WriteFooter(entry.Crc, entry.Compressed, decompressed);
                        writer.streamPosition += entry.Compressed + 16;
                    }
                    writer.entries.Add(entry);
                }
            }

            public override void Flush()
            {
                writeStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                decompressed += (uint) count;
                crc.SlurpBlock(buffer, offset, count);
                writeStream.Write(buffer, offset, count);
            }
        }

        #endregion
    }
}