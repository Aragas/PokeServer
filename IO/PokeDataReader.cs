﻿using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Org.BouncyCastle.Math;
using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace PokeServer.IO
{
    public static class ExtensionMethods
    {
        public static TcpState GetState(this TcpClient tcpClient)
        {
            var foo = IPGlobalProperties.GetIPGlobalProperties()
              .GetActiveTcpConnections()
              .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint));
            return foo != null ? foo.State : TcpState.Unknown;
        }
    }


    // Reads only decrypted data
    public sealed class PokeDataReader : IProtocolDataReader
    {
        private readonly Stream _stream;

        public PokeDataReader(Stream stream)
        {
            _stream = stream;
        }

        public PokeDataReader(byte[] data)
        {
            _stream = new MemoryStream(data);
        }

        // -- String

        public string ReadString(int length = 0)
        {
            length = ReadVarInt();
            var stringBytes = ReadByteArray(length);

            return Encoding.UTF8.GetString(stringBytes);
        }

        // -- VarInt

        public VarInt ReadVarInt()
        {
            var result = 0;
            var length = 0;

            while (true)
            {
                var current = ReadByte();
                result |= (current & 0x7F) << length++ * 7;

                if (length > 6)
                    throw new InvalidDataException("Invalid varint: Too long.");

                if ((current & 0x80) != 0x80)
                    break;
            }

            return result;
        }

        // -- Boolean

        public bool ReadBoolean()
        {
            return Convert.ToBoolean(ReadByte());
        }

        // -- SByte & Byte

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)ReadByte());
        }

        public byte ReadByte()
        {
            return (byte)_stream.ReadByte();
        }

        // -- Short & UShort

        public short ReadShort()
        {
            var bytes = ReadByteArray(2);
            Array.Reverse(bytes);

            return BitConverter.ToInt16(bytes, 0);
        }

        public ushort ReadUShort()
        {
            return (ushort)((ReadByte() << 8) | ReadByte());
        }

        // -- Int & UInt

        public int ReadInt()
        {
            var bytes = ReadByteArray(4);
            Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        public uint ReadUInt()
        {
            return (uint)(
                (ReadUShort() << 24) |
                (ReadUShort() << 16) |
                (ReadUShort() << 8) |
                 ReadUShort());
        }

        // -- Long & ULong

        public long ReadLong()
        {
            var bytes = ReadByteArray(8);
            Array.Reverse(bytes);

            return BitConverter.ToInt64(bytes, 0);
        }

        public ulong ReadULong()
        {
            return unchecked(
                   ((ulong)ReadUShort() << 56) |
                   ((ulong)ReadUShort() << 48) |
                   ((ulong)ReadUShort() << 40) |
                   ((ulong)ReadUShort() << 32) |
                   ((ulong)ReadUShort() << 24) |
                   ((ulong)ReadUShort() << 16) |
                   ((ulong)ReadUShort() << 8) |
                    (ulong)ReadUShort());
        }

        // -- BigInt & UBigInt

        public BigInteger ReadBigInteger()
        {
            var bytes = ReadByteArray(16);
            Array.Reverse(bytes);

            return new BigInteger(bytes);
        }

        public BigInteger ReadUBigInteger()
        {
            throw new NotImplementedException();
        }

        // -- Floats

        public float ReadFloat()
        {
            var bytes = ReadByteArray(4);
            Array.Reverse(bytes);

            return BitConverter.ToSingle(bytes, 0);
        }

        // -- Doubles

        public double ReadDouble()
        {
            var bytes = ReadByteArray(8);
            Array.Reverse(bytes);

            return BitConverter.ToDouble(bytes, 0);
        }


        // -- StringArray

        public string[] ReadStringArray(int value)
        {
            var myStrings = new string[value];

            for (var i = 0; i < value; i++)
                myStrings[i] = ReadString();


            return myStrings;
        }

        // -- VarIntArray

        public int[] ReadVarIntArray(int value)
        {
            var myInts = new int[value];

            for (var i = 0; i < value; i++)
                myInts[i] = ReadVarInt();


            return myInts;
        }

        // -- IntArray

        public int[] ReadIntArray(int value)
        {
            var myInts = new int[value];

            for (var i = 0; i < value; i++)
                myInts[i] = ReadInt();


            return myInts;
        }

        // -- ByteArray

        public byte[] ReadByteArray(int value)
        {
            var myBytes = new byte[value];

            var bytesRead = _stream.Read(myBytes, 0, myBytes.Length);

            while (true)
            {
                if (bytesRead != value)
                {
                    var newSize = value - bytesRead;
                    var bytesRead1 = _stream.Read(myBytes, bytesRead - 1, newSize);

                    if (bytesRead1 != newSize)
                    {
                        value = newSize;
                        bytesRead = bytesRead1;
                    }
                    else break;
                }
                else break;
            }

            return myBytes;
        }


        public void Dispose()
        {
            if (_stream != null)
                _stream.Dispose();
        }
    }
}
