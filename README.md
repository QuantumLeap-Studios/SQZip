![SQZip](https://raw.githubusercontent.com/QuantumLeap-Studios/SQZip/master/Media/sqzip.png)

## Features

- Simple archive format with a clear structure
- Optional per-file Deflate compression
- Fast extraction and creation
- No external dependencies beyond .NET 8
- Preserves directory structure

## Archive Format

- Signature: `SQZIPV01` (8 bytes)
- File count (int32)
- File table size (int32)
- File table: For each file
  - Path length (uint16)
  - Path (UTF-8)
  - File size (int64)
  - Compressed size (int64)
  - Data offset (int64)
  - Is compressed (byte: 1 = compressed, 0 = uncompressed)
- File data section

## Usage

### Creating an Archive
### Extracting an Archive
## API

### `SqzipArchiver.CreateSqzip(string sourceFolder, string destinationFile)`

- Archives all files in `sourceFolder` (recursively) into a single `.sqzip` file.
- Uses Deflate compression only if it reduces file size.

### `SqzipExtractor.ExtractSqzip(string archivePath, string destinationFolder)`

- Extracts all files from the specified `.sqzip` archive into `destinationFolder`.
- Restores original directory structure.

## License

This project is provided as-is, with no warranty. See source code for details.
