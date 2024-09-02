using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable once IdentifierTypo
namespace ssapj.GetFileInformationFromBtwFile
{
    public class GetFileInformationFromBtwFile : IDisposable
    {
        private readonly FileStream _fileStream;
        private byte[] _bufferForHeader;
        private (bool alreadyCheckedHeader, bool isBtwFile) _isBtwFile;

        private readonly byte[] _header =
        {
            0x0d, 0x0a, 0x42, 0x61, 0x72, 0x20, 0x54, 0x65, 0x6e, 0x64, 0x65, 0x72, 0x20, 0x46, 0x6f, 0x72, 0x6d, 0x61,
            0x74, 0x20, 0x46, 0x69, 0x6c, 0x65, 0x0d, 0x0a
        };

        public GetFileInformationFromBtwFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException();
            }

            this._fileStream = File.OpenRead(filePath);
        }

        public async ValueTask<bool> IsBtwFileAsync()
        {
            if (this._isBtwFile.alreadyCheckedHeader)
            {
                return this._isBtwFile.isBtwFile;
            }

            this._bufferForHeader = new byte[26];

            await this._fileStream.ReadAsync(this._bufferForHeader, 0, this._bufferForHeader.Length).ConfigureAwait(false);

            this._isBtwFile = (true, ((IStructuralEquatable)this._bufferForHeader).Equals(this._header, StructuralComparisons.StructuralEqualityComparer));

            return this._isBtwFile.isBtwFile;
        }

        public async ValueTask<IEnumerable<string>> GetBtwFileInformationAsync()
        {
            if (!await this.IsBtwFileAsync())
            {
                throw new InvalidOperationException("This file is not a btw file.");
            }

            var length = 0;

            var bufferForFileInformation = new byte[1023];
            var buffer = new byte[999];
            var crLfLength = 2;
            var copiedHeaderLength = this._header.Length - crLfLength;

            await this._fileStream.ReadAsync(buffer, 0, buffer.Length);

            Buffer.BlockCopy(this._header, crLfLength, bufferForFileInformation, 0, copiedHeaderLength);
            Buffer.BlockCopy(buffer, 0, bufferForFileInformation, copiedHeaderLength, buffer.Length - copiedHeaderLength);

            for (var i = 0; i < bufferForFileInformation.Length - 7; i++)
            {
                if (bufferForFileInformation[i] == 0x89 &&
                    bufferForFileInformation[i + 1] == 0x50 &&
                    bufferForFileInformation[i + 2] == 0x4e &&
                    bufferForFileInformation[i + 3] == 0x47 &&
                    bufferForFileInformation[i + 4] == 0x0d &&
                    bufferForFileInformation[i + 5] == 0x0a &&
                    bufferForFileInformation[i + 6] == 0x1a &&
                    bufferForFileInformation[i + 7] == 0x0a)
                {
                    for (var j = 1; j < i - 1; j++)
                    {
                        if (bufferForFileInformation[i - j] != 0x0A || bufferForFileInformation[i - j - 1] != 0x0d)
                        {
                            continue;
                        }

                        length = i - j - 1;
                        break;
                    }
                }

                if (length != 0)
                {
                    break;
                }
            }

            return SliceAtCrlf(bufferForFileInformation.AsSpan(0, length));
        }

        private static IEnumerable<string> SliceAtCrlf(ReadOnlySpan<byte> readOnlySpan)
        {
            var enc = Encoding.GetEncoding(65001);
            var start = 0;

            var str = new List<string>();

            for (var i = 1; i < readOnlySpan.Length - 1; i++)
            {
                if (readOnlySpan[i] != 0x0d || readOnlySpan[i + 1] != 0x0a)
                {
                    continue;
                }

                str.Add(enc.GetString(readOnlySpan.Slice(start, i - start).ToArray()));
                start = i + 2;
            }

            str.Add(enc.GetString(readOnlySpan.Slice(start).ToArray()));

            return str;
        }

        public void Dispose()
        {
            this._fileStream?.Dispose();
        }

    }
}
