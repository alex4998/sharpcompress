using System;
using System.IO;

using Xunit;

using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Writer;
using SharpCompress.Reader.Zip;
using SharpCompress.Writer.Zip;

namespace SharpCompress.Test
{
    public class ZipWriterTests : WriterTests
    {
        public ZipWriterTests()
            : base(ArchiveType.Zip)
        {
        }

        [Fact]
        public void Zip_Deflate_Write()
        {
            Write(CompressionType.Deflate, "Zip.deflate.noEmptyDirs.zip", "Zip.deflate.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_BZip2_Write()
        {
            Write(CompressionType.BZip2, "Zip.bzip2.noEmptyDirs.zip", "Zip.bzip2.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_None_Write()
        {
            Write(CompressionType.None, "Zip.none.noEmptyDirs.zip", "Zip.none.noEmptyDirs.zip");
        }


        [Fact]
        public void Zip_LZMA_Write()
        {
            Write(CompressionType.LZMA, "Zip.lzma.noEmptyDirs.zip", "Zip.lzma.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_PPMd_Write()
        {
            Write(CompressionType.PPMd, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_Rar_Write()
        {
            Assert.Throws<InvalidFormatException>(() => Write(CompressionType.Rar, "Zip.ppmd.noEmptyDirs.zip", "Zip.ppmd.noEmptyDirs.zip"));
        }

        [Fact]
        public void Zip_WithPassword_Unzip_Without() {
            ResetScratch();
            using(Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(ZipWriter writer = ZipWriter.Open(stream, new CompressionInfo() { Type = CompressionType.Deflate }, null, "test")) {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.False(stream.CanWrite);
            }

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(var reader = ZipReader.Open(stream, null, Options.None)) {
                    Assert.Equal("No password supplied for encrypted zip.", Assert.Throws<CryptographicException>(() => reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath)).Message);
                }
                Assert.False(stream.CanRead);
            }
        }

        [Fact]
        public void Zip_WithPassword_Unzip_WithWrongPassword() {
            ResetScratch();
            using(Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(ZipWriter writer = ZipWriter.Open(stream, new CompressionInfo() { Type = CompressionType.Deflate }, null, "test")) {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.False(stream.CanWrite);
            }

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(var reader = ZipReader.Open(stream, "wrong", Options.None)) {
                    Assert.Equal("The password did not match.", Assert.Throws<CryptographicException>(() => reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath)).Message);
                }
                Assert.False(stream.CanRead);
            }
        }

        [Fact]
        public void Zip_WithoutPassword_Unzip_With() {
            ResetScratch();
            using(Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(ZipWriter writer = ZipWriter.Open(stream, new CompressionInfo() { Type = CompressionType.Deflate }, null)) {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.False(stream.CanWrite);
            }

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, "Zip.deflate.noEmptyDirs.zip"))) {
                using(var reader = ZipReader.Open(stream, "test", Options.None)) {
                    reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath);
                }
                Assert.False(stream.CanRead);
            }
            VerifyFiles();
        }

        [Fact]
        public void Zip_Deflate_Encrypt_Write() {
            WriteWithPassword(CompressionType.Deflate, "test", "Zip.deflate.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_BZip2_Encrypt_Write() {
            WriteWithPassword(CompressionType.BZip2, "test", "Zip.bzip2.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_None_Encrypt_Write() {
            WriteWithPassword(CompressionType.None, "test", "Zip.none.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_LZMA_Encrypt_Write() {
            ResetScratch();
            using(Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, "Zip.lzma.noEmptyDirs.zip"))) {
                using(ZipWriter writer = ZipWriter.Open(stream, new CompressionInfo() { Type = CompressionType.LZMA }, null, "test")) {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.False(stream.CanWrite);
            }

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, "Zip.lzma.noEmptyDirs.zip"))) {
                using(var reader = ZipReader.Open(stream, "test", Options.None)) {
                    Assert.Equal("LZMA with pkware encryption.", Assert.Throws<NotSupportedException>(() => reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath)).Message);
                }
                Assert.False(stream.CanRead);
            }
        }

        [Fact]
        public void Zip_PPMd_Encrypt_Write() {
            WriteWithPassword(CompressionType.PPMd, "test", "Zip.ppmd.noEmptyDirs.zip");
        }

        [Fact]
        public void Zip_Rar_Encrypt_Write() {
            Assert.Throws<InvalidFormatException>(() => WriteWithPassword(CompressionType.Rar, "test", "Zip.deflate.noEmptyDirs.zip"));
        }
    }
}
