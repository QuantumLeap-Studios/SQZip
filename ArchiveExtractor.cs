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

                    if (input.Position + 24 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading file entry metadata (file {i}).");

                    long fileSize = reader.ReadInt64();
                    long compressedSize = reader.ReadInt64();
                    long dataOffset = reader.ReadInt64();

                    if (input.Position + 1 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading isCompressed (file {i}).");
                    bool isCompressed = reader.ReadByte() == 1;

                    if (input.Position + 1 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading isDirectory (file {i}).");
                    bool isDirectory = reader.ReadByte() == 1;

                    if (input.Position + 32 > fileTableEnd)
                        throw new EndOfStreamException($"Unexpected end of file table while reading file hash (file {i}).");
                    byte[] hash = reader.ReadBytes(32);

                    if (fileSize < 0 || compressedSize < 0 || dataOffset < 0)
                        throw new Exception($"Invalid file metadata (file {i}): fileSize={fileSize}, compressedSize={compressedSize}, dataOffset={dataOffset}");

                    fileEntries.Add(new SqzipFileEntry
                    {
                        FilePath = filePath,
                        FileSize = fileSize,
                        CompressedSize = compressedSize,
                        DataOffset = dataOffset,
                        IsCompressed = isCompressed,
                        Hash = hash,
                        IsDirectory = isDirectory
                    });
                }

                foreach (var entry in fileEntries)
                {
                    string outputPath = Path.Combine(destinationFolder, entry.FilePath);

                    if (entry.IsDirectory)
                    {
                        Directory.CreateDirectory(outputPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                    input.Seek(entry.DataOffset, SeekOrigin.Begin);
                    byte[] data = reader.ReadBytes((int)entry.CompressedSize);

                    byte[] extractedData;
                    if (entry.IsCompressed)
                    {
                        using var compressedStream = new MemoryStream(data);
                        using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                        using var ms = new MemoryStream();
                        deflateStream.CopyTo(ms);
                        extractedData = ms.ToArray();
                    }
                    else
                    {
                        extractedData = data;
                    }

                    File.WriteAllBytes(outputPath, extractedData);

                    using var sha256 = System.Security.Cryptography.SHA256.Create();
                    var actualHash = sha256.ComputeHash(extractedData);
                    if (!entry.Hash.AsSpan().SequenceEqual(actualHash))
                        throw new Exception($"Hash mismatch for file {entry.FilePath}: file may be corrupted.");
                }
            }
        }
    }
}
