using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SQZip
{
    public class SqzipExtractor
    {
        public static void ExtractSqzip(string archivePath, string destinationFolder)
        {
            using (var input = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
            {
                if (input.Length < 16)           
                    throw new Exception("Invalid or corrupted SQZIP archive: file too small.");

                string signature = Encoding.ASCII.GetString(reader.ReadBytes(8));
                if (signature != "SQZIPV01")
                    throw new Exception("Invalid SQZIP file: bad signature.");

                int fileCount = reader.ReadInt32();
                int fileTableSize = reader.ReadInt32();

                if (fileCount < 0)
                    throw new Exception("Invalid file count in archive.");

                if (fileTableSize <= 0 || fileTableSize > input.Length - input.Position)
                    throw new Exception($"Invalid file table size: {fileTableSize}");

                var fileEntries = new List<SqzipFileEntry>();

                long fileTableEnd = input.Position + fileTableSize;

                for (int i = 0; i < fileCount; i++)
                {
                    if (input.Position + 2 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading path length (file {i}).");

                    int pathLength = reader.ReadUInt16();

                    if (pathLength == 0 || input.Position + pathLength > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading file path (file {i}).");

                    string filePath = Encoding.UTF8.GetString(reader.ReadBytes(pathLength));

                    if (input.Position + 25 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading file entry metadata (file {i}).");

                    long fileSize = reader.ReadInt64();
                    long compressedSize = reader.ReadInt64();
                    long dataOffset = reader.ReadInt64();
                    bool isCompressed = reader.ReadByte() == 1;

                    if (fileSize < 0 || compressedSize < 0 || dataOffset < 0)
                        throw new Exception($"Invalid file metadata (file {i}).");

                    fileEntries.Add(new SqzipFileEntry
                    {
                        FilePath = filePath,
                        FileSize = fileSize,
                        CompressedSize = compressedSize,
                        DataOffset = dataOffset,
                        IsCompressed = isCompressed
                    });
                }

                foreach (var entry in fileEntries)
                {
                    string outputPath = Path.Combine(destinationFolder, entry.FilePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new Exception("Invalid output directory."));

                    if (entry.DataOffset < 0 || entry.DataOffset > input.Length)
                        throw new Exception($"Invalid data offset for file {entry.FilePath}.");

                    input.Seek(entry.DataOffset, SeekOrigin.Begin);

                    if (entry.CompressedSize > int.MaxValue)
                        throw new InvalidOperationException($"Compressed file too large to extract in memory: {entry.FilePath}");

                    if (input.Position + entry.CompressedSize > input.Length)
                        throw new EndOfStreamException($"Unexpected end of archive while reading data for file {entry.FilePath}.");

                    byte[] data = reader.ReadBytes((int)entry.CompressedSize);

                    if (entry.IsCompressed)
                    {
                        using (var compressedStream = new MemoryStream(data))
                        using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                        using (var fileStream = new FileStream(outputPath, FileMode.Create))
                        {
                            deflateStream.CopyTo(fileStream);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(outputPath, data);
                    }
                }
            }
        }
    }
}
