using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;

namespace SQZip
{
    class SqzipFileEntry
    {
        public string FilePath = string.Empty;
        public long FileSize;
        public long CompressedSize;
        public long DataOffset;
        public bool IsCompressed;
        public bool IsDirectory;
        public byte[] Hash = Array.Empty<byte>();
    }

    public class SqzipArchiver
    {
        public static void CreateSqzip(string sourceFolder, string destinationFile)
        {
            var files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            var directories = Directory.GetDirectories(sourceFolder, "*", SearchOption.AllDirectories);

            var fileEntries = new List<SqzipFileEntry>();

            foreach (var dir in directories)
            {
                var entry = new SqzipFileEntry
                {
                    FilePath = Path.GetRelativePath(sourceFolder, dir).Replace("\\", "/") + "/",
                    FileSize = 0,
                    CompressedSize = 0,
                    DataOffset = 0,
                    IsCompressed = false,
                    IsDirectory = true,
                    Hash = new byte[32]   
                };
                fileEntries.Add(entry);
            }

            foreach (var file in files)
            {
                var entry = new SqzipFileEntry
                {
                    FilePath = Path.GetRelativePath(sourceFolder, file).Replace("\\", "/"),
                    FileSize = new FileInfo(file).Length,
                    IsDirectory = false
                };

                using (var sha256 = SHA256.Create())
                using (var fs = File.OpenRead(file))
                {
                    entry.Hash = sha256.ComputeHash(fs);
                }

                fileEntries.Add(entry);
            }

            fileEntries.Sort((a, b) => string.Compare(a.FilePath, b.FilePath, StringComparison.Ordinal));

            using (var output = new FileStream(destinationFile, FileMode.Create))
            using (var writer = new BinaryWriter(output, Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("SQZIPV01"));
                writer.Write(0);        
                writer.Write(0);         

                long fileTableStart = output.Position;

                using (var tableStream = new MemoryStream())
                using (var tableWriter = new BinaryWriter(tableStream, Encoding.UTF8))
                {
                    foreach (var entry in fileEntries)
                    {
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.FilePath);
                        tableWriter.Write((ushort)pathBytes.Length);
                        tableWriter.Write(pathBytes);
                        tableWriter.Write((long)entry.FileSize);
                        tableWriter.Write((long)0);       
                        tableWriter.Write((long)0);       
                        tableWriter.Write((byte)(entry.IsCompressed ? 1 : 0));
                        tableWriter.Write((byte)(entry.IsDirectory ? 1 : 0));
                        tableWriter.Write(entry.Hash, 0, 32);     
                    }

                    byte[] fileTableBytes = tableStream.ToArray();
                    writer.Write(fileTableBytes);

                    for (int i = 0; i < fileEntries.Count; i++)
                    {
                        var entry = fileEntries[i];
                        if (entry.IsDirectory)
                        {
                            entry.CompressedSize = 0;
                            entry.DataOffset = 0;
                            entry.IsCompressed = false;
                            continue;
                        }

                        entry.DataOffset = output.Position;

                        byte[] fileData = File.ReadAllBytes(Path.Combine(sourceFolder, entry.FilePath));

                        using (var ms = new MemoryStream())
                        {
                            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
                            {
                                ds.Write(fileData, 0, fileData.Length);
                            }

                            byte[] compressedData = ms.ToArray();

                            if (compressedData.Length < fileData.Length)
                            {
                                entry.CompressedSize = compressedData.Length;
                                entry.IsCompressed = true;
                                writer.Write(compressedData);
                            }
                            else
                            {
                                entry.CompressedSize = entry.FileSize;
                                entry.IsCompressed = false;
                                writer.Write(fileData);
                            }
                        }
                    }

                    long dataSectionEnd = output.Position;

                    output.Seek(fileTableStart, SeekOrigin.Begin);
                    foreach (var entry in fileEntries)
                    {
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.FilePath);
                        writer.Write((ushort)pathBytes.Length);
                        writer.Write(pathBytes);
                        writer.Write(entry.FileSize);
                        writer.Write(entry.CompressedSize);
                        writer.Write(entry.DataOffset);
                        writer.Write((byte)(entry.IsCompressed ? 1 : 0));
                        writer.Write((byte)(entry.IsDirectory ? 1 : 0));
                        writer.Write(entry.Hash, 0, 32);
                    }

                    output.Seek(8, SeekOrigin.Begin);
                    writer.Write(fileEntries.Count);
                    writer.Write((int)(dataSectionEnd - fileTableStart));
                }
            }
        }
    }
}
