using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PRISM
{
    /// <summary>
    /// A stream wrapper for GZip files that makes the GZip file metadata available to read or write
    /// </summary>
    internal class GZipMetadataStream : Stream
    {
        [Flags]
        private enum GzipFlags : byte
        {
            FTEXT = 0x1,
            FHCRC = 0x2,
            FEXTRA = 0x4,
            FNAME = 0x8,
            FCOMMENT = 0x10,
            Reserved1 = 0x20,
            Reserved2 = 0x40,
            Reserved3 = 0x80,
        }

        /// <summary>
        /// Constructor for writing
        /// </summary>
        /// <param name="baseStream">output stream</param>
        /// <param name="internalLastModified">LastWriteTime for the file being compressed</param>
        /// <param name="internalFilename">filename of the file being compressed. Do NOT include path components!</param>
        /// <param name="internalComment">Comment to store to file metadata</param>
        /// <param name="addHeaderCrc">if true, write a CRC16 for the header to the metadata</param>
        public GZipMetadataStream(Stream baseStream, DateTime internalLastModified, string internalFilename = null, string internalComment = null, bool addHeaderCrc = false)
        {
            BaseStream = baseStream;
            InternalFilename = internalFilename;
            InternalLastModified = internalLastModified;
            InternalComment = internalComment;
            AddOrCheckHeaderCrc = addHeaderCrc;
            isWriter = true;
        }

        /// <summary>
        /// Constructor for writing
        /// </summary>
        /// <param name="baseStream">output stream</param>
        /// <param name="inputFile">file being compressed, used for last write time and filename</param>
        /// <param name="internalComment">Comment to store to file metadata</param>
        /// <param name="addHeaderCrc">if true, write a CRC16 for the header to the metadata</param>
        public GZipMetadataStream(Stream baseStream, FileInfo inputFile, string internalComment = null, bool addHeaderCrc = false)
        {
            BaseStream = baseStream;
            InternalFilename = inputFile.Name;
            InternalLastModified = inputFile.LastWriteTime;
            InternalComment = internalComment;
            AddOrCheckHeaderCrc = addHeaderCrc;
            isWriter = true;
        }

        /// <summary>
        /// Constructor for reading
        /// </summary>
        /// <param name="baseStream">Stream to wrap; must be seek-able (CanSeek == true)</param>
        /// <param name="checkHeaderCrc"></param>
        public GZipMetadataStream(Stream baseStream, bool checkHeaderCrc = false)
        {
            BaseStream = baseStream;
            AddOrCheckHeaderCrc = checkHeaderCrc;
            HeaderCorrupted = ReadMetadata();
        }

        /// <summary>
        /// Stream for reading/writing the gzip file
        /// </summary>
        public Stream BaseStream { get; }

        /// <summary>
        /// Filename stored in the gzip metadata. 'null' or 'empty' means 'not set'
        /// </summary>
        public string InternalFilename { get; private set; }

        /// <summary>
        /// Comment stored in the gzip metadata. 'null' or 'empty' means 'not set'
        /// </summary>
        public string InternalComment { get; private set; }

        /// <summary>
        /// Date modified stored in the gzip metadata. 'DateTime.MinValue' or 'DateTime(1970, 1, 1, 0, 0, 0)' mean 'not set'; 'DateTime.MinValue' is always returned for 'not set' when reading.
        /// </summary>
        public DateTime InternalLastModified { get; private set; }

        /// <summary>
        /// If true, a header CRC16 will be added when writing
        /// </summary>
        public bool AddOrCheckHeaderCrc { get; }

        /// <summary>
        /// Header CRC16 value stored to or read from the gzip metadata
        /// </summary>
        public ushort HeaderCrc { get; private set; }

        /// <summary>
        /// (Read gzip only) True if the header CRC16 did not match the stored values
        /// </summary>
        public bool HeaderCorrupted { get; }

        /// <summary>
        /// (Read gzip only) True if the gzip headers were read. If false, it usually means BaseStream.CanSeek is false.
        /// </summary>
        public bool HeaderRead { get; private set; }

        private readonly bool isWriter = false;

        /// <inheritdoc />
        public override void Flush()
        {
            BaseStream.Flush();
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            BaseStream.SetLength(value);
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
        }

        private bool ReadMetadata()
        {
            if (!CanSeek)
            {
                // Can't seek, then don't read metadata, because it will cause the GZipStream to fail.
                HeaderRead = false;
                return false;
            }

            var id1 = BaseStream.ReadByte(); // should be 31/0x1f (GZIP constant)
            var id2 = BaseStream.ReadByte(); // should be 139/0x8b (GZIP constant)
            var compressionMethod = BaseStream.ReadByte(); // should be 8 (deflate)
            var
                flags = (GzipFlags)BaseStream
                    .ReadByte(); // bit flags; &0x1 = FTEXT (probably ASCII), &0x2 = CRC16 for gzip header present, &0x4 = FEXTRA (extra fields), &0x8 = FNAME (name), &0x10 = FCOMMENT (comment), &0x20, &0x40, &0x80 = reserved

            uint timestamp = 0;
            for (int x = 0; x < 4; x++)
            {
                timestamp += (uint)BaseStream.ReadByte() << (8 * x);
            }

            // if timestamp == 0, no timestamp is available; use the compressed file timestamp
            if (timestamp > 0)
            {
                InternalLastModified = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp); // Gzip times are stored in universal time
            }
            else
            {
                InternalLastModified = DateTime.MinValue;
            }

            var extraFlags = BaseStream.ReadByte();
            var osId = BaseStream.ReadByte();

            if (flags.HasFlag(GzipFlags.FEXTRA))
            {
                var length = BaseStream.ReadByte();
                length += BaseStream.ReadByte() << 8;
                BaseStream.Seek(length, SeekOrigin.Current);
            }

            /*
             * FNAME and FCOMMENT both must be written (and read) as ISO-8859-1 (LATIN-1) characters
             * FNAME is filename only (no path), and if written on case-insensitive file system, lower-case only,
             */

            var iso8859Encoding = Encoding.GetEncoding("ISO-8859-1");
            if (flags.HasFlag(GzipFlags.FNAME))
            {
                var nameBytes = new List<byte>();
                int c;
                while ((c = BaseStream.ReadByte()) > 0)
                {
                    nameBytes.Add((byte)c);
                }

                InternalFilename = iso8859Encoding.GetString(nameBytes.ToArray());
            }

            if (flags.HasFlag(GzipFlags.FCOMMENT))
            {
                var commentbytes = new List<byte>();
                int c;
                while ((c = BaseStream.ReadByte()) > 0)
                {
                    commentbytes.Add((byte)c);
                }

                InternalComment = iso8859Encoding.GetString(commentbytes.ToArray());
            }

            var headerCorrupted = false;
            if (flags.HasFlag(GzipFlags.FHCRC))
            {
                var headerCrcPosition = (int)BaseStream.Position;
                // Applies to all bytes prior to this item
                var headerCrcBytes = new byte[2];
                headerCrcBytes[0] = (byte)BaseStream.ReadByte();
                headerCrcBytes[1] = (byte)BaseStream.ReadByte();
                HeaderCrc = (ushort)(headerCrcBytes[0] + (headerCrcBytes[1] << 8));

                BaseStream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[headerCrcPosition];
                BaseStream.Read(bytes, 0, headerCrcPosition);

                var crc = Crc32Gzip.Crc(bytes);
                var crc16 = (ushort)crc;
                headerCorrupted = HeaderCrc != crc16;
            }

            // Then compressed data
            // Then 4 CRC32 bytes, then 4 ISIZE bytes

            // Reset stream position before decompressing file
            BaseStream.Seek(0, SeekOrigin.Begin);

            HeaderRead = true;
            return headerCorrupted;
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Implementation caveat: The .NET GZipStream/DeflateStream implementation, as of .NET 4.7.1, writes the entire header at once. Right now we depend on that functionality.

            if (Position == 0 && count >= 10)
            {
                var modifiedBuffer = new List<byte>();
                var i = offset;
                modifiedBuffer.Add(buffer[i++]); // should be 31/0x1f (GZIP constant)
                modifiedBuffer.Add(buffer[i++]); // should be 139/0x8b (GZIP constant)
                modifiedBuffer.Add(buffer[i++]); // should be 8 (deflate)
                var flags = (GzipFlags)buffer[i++];
                var writeFileName = false;
                if (!flags.HasFlag(GzipFlags.FNAME) && !string.IsNullOrWhiteSpace(InternalFilename))
                {
                    writeFileName = true;
                    flags = flags | GzipFlags.FNAME;
                }

                var writeComment = false;
                if (!flags.HasFlag(GzipFlags.FCOMMENT) && !string.IsNullOrWhiteSpace(InternalComment))
                {
                    writeComment = true;
                    flags = flags | GzipFlags.FCOMMENT;
                }

                var addingHeaderCrc = true;
                if (AddOrCheckHeaderCrc)
                {
                    if (flags.HasFlag(GzipFlags.FHCRC))
                    {
                        addingHeaderCrc = false;
                    }

                    flags = flags | GzipFlags.FHCRC;
                }
                modifiedBuffer.Add((byte)flags);

                var unixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                if (InternalLastModified.ToUniversalTime() > unixTimeStart)
                {
                    i += 4;
                    var utc = InternalLastModified.ToUniversalTime();
                    var timestampUnix = (long)utc.Subtract(unixTimeStart).TotalSeconds;

                    modifiedBuffer.Add((byte)((timestampUnix & 0x000000FF)));
                    modifiedBuffer.Add((byte)((timestampUnix & 0x0000FF00) >> 8));
                    modifiedBuffer.Add((byte)((timestampUnix & 0x00FF0000) >> 16));
                    modifiedBuffer.Add((byte)((timestampUnix & 0xFF000000) >> 24));
                }
                else
                {
                    modifiedBuffer.Add(buffer[i++]);
                    modifiedBuffer.Add(buffer[i++]);
                    modifiedBuffer.Add(buffer[i++]);
                    modifiedBuffer.Add(buffer[i++]);
                }

                modifiedBuffer.Add(buffer[i++]); // Extra Fields byte
                modifiedBuffer.Add(buffer[i++]); // OS byte

                if (flags.HasFlag(GzipFlags.FEXTRA))
                {
                    int length = buffer[i];
                    modifiedBuffer.Add(buffer[i++]);
                    length += (int)buffer[i] << 8;
                    modifiedBuffer.Add(buffer[i++]);

                    for (var j = 0; j < length && i < offset + count; j++)
                    {
                        modifiedBuffer.Add(buffer[i++]);
                    }
                }

                var iso8859Encoding = Encoding.GetEncoding("ISO-8859-1");
                if (writeFileName)
                {
                    var bytes = iso8859Encoding.GetBytes(InternalFilename);
                    modifiedBuffer.AddRange(bytes);
                    modifiedBuffer.Add(0); // add zero-byte terminator
                }

                if (writeComment)
                {
                    var bytes = iso8859Encoding.GetBytes(InternalComment);
                    modifiedBuffer.AddRange(bytes);
                    modifiedBuffer.Add(0); // add zero-byte terminator
                }

                if (flags.HasFlag(GzipFlags.FHCRC))
                {
                    if (!addingHeaderCrc)
                    {
                        i += 2;
                    }

                    // least two significant bytes of the CRC32 for all bytes of the gzip header, up to (but not including) the CRC16
                    var crc = Crc32Gzip.Crc(modifiedBuffer.ToArray());
                    modifiedBuffer.Add((byte)((crc & 0x000000FF)));
                    modifiedBuffer.Add((byte)((crc & 0x0000FF00) >> 8));
                    HeaderCrc = (ushort) crc;
                }

                for (var j = i; j < offset + count; j++)
                {
                    modifiedBuffer.Add(buffer[j]);
                }

                BaseStream.Write(modifiedBuffer.ToArray(), 0, modifiedBuffer.Count);
            }
            else
            {
                BaseStream.Write(buffer, offset, count);
            }
        }

        /// <inheritdoc />
        public override bool CanRead => BaseStream.CanRead && !isWriter;

        /// <inheritdoc />
        public override bool CanSeek => BaseStream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => BaseStream.CanWrite && isWriter;

        /// <inheritdoc />
        public override long Length => BaseStream.Length;

        /// <inheritdoc />
        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        private static class Crc32Gzip
        {
            // https://tools.ietf.org/html/rfc1952

            /// <summary>
            /// Table of CRCs of all 8-bit messages.
            /// </summary>
            private static readonly uint[] crcTable = new uint[256];

            /// <summary>
            /// Flag: has the table been computed? Initially false.
            /// </summary>
            private static bool crcTableComputed = false;

            /// <summary>
            /// Make the table for a fast CRC.
            /// </summary>
            private static void MakeCrcTable()
            {
                for (var n = 0; n < 256; n++)
                {
                    var c = (uint)n;
                    for (var k = 0; k < 8; k++)
                    {
                        if ((c & 1) != 0)
                        {
                            c = 0xedb88320 ^ (c >> 1);
                        }
                        else
                        {
                            c = c >> 1;
                        }
                    }

                    crcTable[n] = c;
                }

                crcTableComputed = true;
            }

            /// <summary>
            /// Update a running crc with the bytes buf[0..len-1] and return the updated crc. The crc should be initialized to zero. Pre- and post-conditioning (one's complement) is performed within this function so it shouldn't be done by the caller.
            /// </summary>
            /// <param name="crc"></param>
            /// <param name="buf"></param>
            /// <param name="len"></param>
            /// <returns></returns>
            /// <example>
            /// uint crc = 0;
            ///
            /// while (readBuffer(buffer, length) != 0)
            /// {
            ///     crc = UpdateCrc(crc, buffer, length);
            /// }
            /// if (crc != originalCrc) error();
            /// </example>
            public static uint UpdateCrc(uint crc, byte[] buf, int len)
            {
                uint c = crc ^ 0xffffffff;

                if (!crcTableComputed)
                    MakeCrcTable();

                for (var n = 0; n < len; n++)
                {
                    c = crcTable[(c ^ buf[n]) & 0xff] ^ (c >> 8);
                }
                return c ^ 0xffffffff;
            }

            /// <summary>
            /// Return the CRC of the bytes buf[0..len-1].
            /// </summary>
            /// <param name="buf"></param>
            /// <param name="len"></param>
            /// <returns></returns>
            public static uint Crc(byte[] buf, int len)
            {
                return UpdateCrc(0, buf, len);
            }

            /// <summary>
            /// Update a running crc with the byte array and return the updated crc. The crc should be initialized to zero. Pre- and post-conditioning (one's complement) is performed within this function so it shouldn't be done by the caller.
            /// </summary>
            /// <param name="crc"></param>
            /// <param name="buf"></param>
            /// <returns></returns>
            public static uint UpdateCrc(uint crc, byte[] buf)
            {
                return UpdateCrc(crc, buf, buf.Length);
            }

            /// <summary>
            /// Return the CRC of the byte array
            /// </summary>
            /// <param name="buf"></param>
            /// <returns></returns>
            public static uint Crc(byte[] buf)
            {
                return Crc(buf, buf.Length);
            }
        }
    }
}
