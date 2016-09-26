using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Zip;
using SharpCompress.Writer;
using SharpCompress.Writer.Zip;
using Xunit;

namespace SharpCompress.Test
{
    public class WriterTests : TestBase
    {
        private ArchiveType type;

        protected WriterTests(ArchiveType type)
        {
            this.type = type;
        }

        protected void Write(CompressionType compressionType, string archive, string archiveToVerifyAgainst)
        {
            ResetScratch();
            using (Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive)))
            {
                using (var writer = WriterFactory.Open(stream, type, compressionType, true))
                {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.True(stream.CanWrite);
            }
            CompareArchivesByPath(Path.Combine(SCRATCH2_FILES_PATH, archive),
               Path.Combine(TEST_ARCHIVES_PATH, archiveToVerifyAgainst));

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive))) {
                using(var reader = ReaderFactory.Open(stream)) {
                    reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath);
                }
                Assert.True(stream.CanRead);
            }
            VerifyFiles();
        }

        protected void WriteWithPassword(CompressionType compressionType, string password, string archive) {
            ResetScratch();
            using(Stream stream = File.OpenWrite(Path.Combine(SCRATCH2_FILES_PATH, archive))) {
                using(ZipWriter writer = ZipWriter.Open(stream, new CompressionInfo() { Type = compressionType }, null, password)) {
                    writer.WriteAll(ORIGINAL_FILES_PATH, "*", SearchOption.AllDirectories);
                }
                Assert.False(stream.CanWrite);
            }

            using(Stream stream = File.OpenRead(Path.Combine(SCRATCH2_FILES_PATH, archive))) {
                using(var reader = ZipReader.Open(stream, "test", Options.None)) {
                    reader.WriteAllToDirectory(SCRATCH_FILES_PATH, ExtractOptions.ExtractFullPath);
                }
                Assert.False(stream.CanRead);
            }
            VerifyFiles();
        }
    }
}
