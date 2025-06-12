using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace PRISM
{
    /// <summary>
    /// Implementation of Crc32 in https://tools.ietf.org/html/rfc1952
    /// </summary>
    [CLSCompliant(false)]
    public static class Crc32
    {
        // Ignore Spelling: buf, crc, pre, uint

        /// <summary>
        /// Table of CRCs of all 8-bit messages
        /// </summary>
        private static readonly uint[] crcTable = new uint[256];

        /// <summary>
        /// Set to true once the crcTable has been populated
        /// </summary>
        private static bool crcTableComputed;

        /// <summary>
        /// Populate crcTable, which will allow for quickly computing CRC values
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
                        c >>= 1;
                    }
                }

                crcTable[n] = c;
            }

            crcTableComputed = true;
        }

        /// <summary>
        /// Update a running CRC using the enumerable byte buffer;
        /// the crc should be initialized to zero.
        /// </summary>
        /// <remarks>
        /// Pre- and post-conditioning (one's complement) is performed within this method, so it shouldn't be done by the caller
        /// </remarks>
        /// <example>
        /// uint crc = 0;
        ///
        /// while (readBuffer(buffer, length) != 0)
        /// {
        ///     crc = UpdateCrc(crc, buffer, length);
        /// }
        /// if (crc != originalCrc) error();
        /// </example>
        /// <param name="crc">CRC</param>
        /// <param name="buf">Byte buffer</param>
        public static uint UpdateCrc(uint crc, IEnumerable<byte> buf)
        {
            var c = crc ^ 0xffffffff;

            if (!crcTableComputed)
                MakeCrcTable();

            foreach (var b in buf)
            {
                c = crcTable[(c ^ b) & 0xff] ^ (c >> 8);
            }
            return c ^ 0xffffffff;
        }

        /// <summary>
        /// Return the CRC32 of the enumerable byte buffer
        /// </summary>
        /// <param name="buf">Byte buffer</param>
        public static uint Crc(IEnumerable<byte> buf)
        {
            return UpdateCrc(0, buf);
        }

        /// <summary>
        /// Return the CRC32 of the byte stream
        /// </summary>
        /// <param name="stream">Byte stream</param>
        public static uint Crc(Stream stream)
        {
            const int BUFFER_SIZE = 65536;

            var buffer = new byte[BUFFER_SIZE];
            uint crc = 0;

            int count;

            while ((count = stream.Read(buffer, 0, BUFFER_SIZE)) > 0)
            {
                crc = UpdateCrc(crc, buffer.Take(count));
            }

            return crc;
        }
    }
}
