﻿using System;
using System.IO;
using System.Text;
using Org.BouncyCastle.Math;
using Poke.Core.Data;
using Poke.Core.Interfaces;
using Poke.Server.Packets;

namespace Poke.Server.IO
{
    // -- Credits to umby24 for encryption support, as taken from CWrapped.
    // -- Credits to SirCmpwn for encryption support, as taken from SMProxy.
    // -- All Write methods doesn't write to any stream. It writes to _buffer. Purge write _buffer to any stream.
    public sealed class PlayerStream : IProtocolStreamExtended
    {
        public NetworkMode NetworkMode { get; set; }

        private delegate IAsyncResult PacketWrite(IPacket packet);
        private PacketWrite _packetWriteDelegate;

        public bool EncryptionEnabled { get; private set; }

        public int Available { get; set; }

        private Stream _stream;
        private IAesStream _aesStream;
        private byte[] _buffer;
        private Encoding _encoding = Encoding.UTF8;

        public PlayerStream(Stream stream)
        {
            _stream = stream;
        }

        public void InitializeEncryption(byte[] key)
        {
            _aesStream = new BouncyCastleAesStream(_stream, key);

            EncryptionEnabled = true;
        }


        // -- String

        public void WriteString(string value, int length = 0)
        {
            var lengthBytes = GetVarIntBytes(value.Length);
            var final = new byte[value.Length + lengthBytes.Length];

            Buffer.BlockCopy(lengthBytes, 0, final, 0, lengthBytes.Length);
            Buffer.BlockCopy(_encoding.GetBytes(value), 0, final, lengthBytes.Length, value.Length);

            WriteByteArray(final);
        }

        // -- VarInt

        public void WriteVarInt(VarInt value)
        {
            WriteByteArray(GetVarIntBytes(value));
        }

        public static byte[] GetVarIntBytes(long value)
        {
            var byteBuffer = new byte[10];
            short pos = 0;

            do
            {
                var byteVal = (byte)(value & 0x7F);
                value >>= 7;

                if (value != 0)
                    byteVal |= 0x80;

                byteBuffer[pos] = byteVal;
                pos += 1;
            } while (value != 0);

            var result = new byte[pos];
            Buffer.BlockCopy(byteBuffer, 0, result, 0, pos);

            return result;
        }

        // -- Boolean

        public void WriteBoolean(bool value)
        {
            WriteByte(Convert.ToByte(value));
        }

        // -- SByte & Byte

        public void WriteSByte(sbyte value)
        {
            WriteByte(unchecked((byte)value));
        }

        public void WriteByte(byte value)
        {
            if (_buffer != null)
            {
                var tempBuff = new byte[_buffer.Length + 1];

                Buffer.BlockCopy(_buffer, 0, tempBuff, 0, _buffer.Length);
                tempBuff[_buffer.Length] = value;

                _buffer = tempBuff;
            }
            else
                _buffer = new byte[] { value };
        }

        // -- Short & UShort

        public void WriteShort(short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteUShort(ushort value)
        {
            WriteByteArray(new byte[]
            {
                (byte) ((value & 0xFF00) >> 8),
                (byte) (value & 0xFF)
            });
        }

        // -- Int & UInt

        public void WriteInt(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteUInt(uint value)
        {
            WriteByteArray(new[]
            {
                (byte)((value & 0xFF000000) >> 24),
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF)
            });
        }

        // -- Long & ULong

        public void WriteLong(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteULong(ulong value)
        {
            WriteByteArray(new[]
            {
                (byte)((value & 0xFF00000000000000) >> 56),
                (byte)((value & 0xFF000000000000) >> 48),
                (byte)((value & 0xFF0000000000) >> 40),
                (byte)((value & 0xFF00000000) >> 32),
                (byte)((value & 0xFF000000) >> 24),
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF)
            });
        }

        // -- BigInt & UBigInt

        public void WriteBigInteger(BigInteger value)
        {
            var bytes = value.ToByteArray();
            Array.Reverse(bytes);
        
            WriteByteArray(bytes);
        }

        public void WriteUBigInteger(BigInteger value)
        {
            throw new NotImplementedException();
        }

        // -- Float

        public void WriteFloat(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        // -- Double

        public void WriteDouble(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }


        // -- StringArray

        public void WriteStringArray(string[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteString(value[i]);
        }

        // -- VarIntArray

        public void WriteVarIntArray(int[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteVarInt(value[i]);
        }

        // -- IntArray

        public void WriteIntArray(int[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteInt(value[i]);
        }

        // -- ByteArray

        public void WriteByteArray(byte[] value)
        {
            if (_buffer != null)
            {
                var tempLength = _buffer.Length + value.Length;
                var tempBuff = new byte[tempLength];

                Buffer.BlockCopy(_buffer, 0, tempBuff, 0, _buffer.Length);
                Buffer.BlockCopy(value, 0, tempBuff, _buffer.Length, value.Length);

                _buffer = tempBuff;
            }
            else
                _buffer = value;
        }


        // -- Read methods

        public byte ReadByte()
        {
            if (EncryptionEnabled)
                return (byte)_aesStream.ReadByte();
            else
                return (byte)_stream.ReadByte();
        }

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

        public byte[] ReadByteArray(int value)
        {
            if (!EncryptionEnabled)
            {
                var result = new byte[value];
                if (value == 0) return result;
                int n = value;
                while (true)
                {
                    n -= _stream.Read(result, value - n, n);
                    if (n == 0)
                        break;
                }
                return result;
            }
            else
            {
                var result = new byte[value];
                if (value == 0) return result;
                int n = value;
                while (true)
                {
                    n -= _aesStream.Read(result, value - n, n);
                    if (n == 0)
                        break;
                }
                return result;
            }
        }


        public void SendPacket(IPacket packet)
        {
            using (var ms = new MemoryStream())
            using (var stream = new PlayerStream(ms))
            {
                packet.WritePacket(stream);
                var data = ms.ToArray();


                if (EncryptionEnabled)
                    _aesStream.Write(data, 0, data.Length);
                else
                    _stream.Write(data, 0, data.Length);
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (EncryptionEnabled)
                return _aesStream.Read(buffer, offset, count);
            else
                return _stream.Read(buffer, offset, count);
        }

        public IPacket ReadPacket()
        {
            var packetLength = ReadVarInt();
            if (packetLength == 0)
                throw new Exception("Reading error: Packet Length size is 0");

            var packetId = ReadVarInt();
            var data = ReadByteArray(packetLength - 1);

            using (var reader = new PokeDataReader(data))
            {
                switch (NetworkMode)
                {
                    case NetworkMode.Handshake:
                        if (packetId != 0x00)
                            throw new Exception("Reading error: Not a handshake");

                        
                        var packet = (HandshakePacket) new HandshakePacket().ReadPacket(reader);
                        switch (packet.ConnectionMode)
                        {
                            case 0x00: // -- Client wanna play
                                NetworkMode = NetworkMode.JoiningServer;
                                break;

                            case 0x01: // -- Client wanna get some shitty info
                                NetworkMode = NetworkMode.ShittyInfo;
                                break;

                            default: // -- Client is broken
                                throw new Exception("Reading error: Handshake ConnectionMode readig error");
                        }
                        return packet;

                    case NetworkMode.JoiningServer:
                        return ClientResponse.JoiningServer[packetId]().ReadPacket(reader);

                    case NetworkMode.JoinedServer:
                        return ClientResponse.JoinedServer[packetId]().ReadPacket(reader);

                    case NetworkMode.ShittyInfo:
                        return ClientResponse.InfoRequest[packetId]().ReadPacket(reader);
                }
            }

            throw new Exception("Reading error: WTF");
        }


        public IAsyncResult BeginSendPacket(IPacket packet, AsyncCallback callback, object state)
        {
            _packetWriteDelegate = packet1 =>
            {
                using (var ms = new MemoryStream())
                using (var stream = new PlayerStream(ms))
                {
                    packet.WritePacket(stream);
                    var data = ms.ToArray();

                    return BeginSend(data, null, null);
                }
            };

            return _packetWriteDelegate.BeginInvoke(packet, callback, state);
        }

        public IAsyncResult BeginSend(byte[] data, AsyncCallback callback, object state)
        {
            if (EncryptionEnabled)
                return _aesStream.BeginWrite(data, 0, data.Length, callback, state);
            else
                return _stream.BeginWrite(data, 0, data.Length, callback, state);
        }

        public void EndSend(IAsyncResult asyncResult)
        {
            try { _packetWriteDelegate.EndInvoke(asyncResult); }
            catch (Exception) { }
        }


        public IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (EncryptionEnabled)
                return _aesStream.BeginRead(buffer, offset, count, callback, state);
            else
                return _stream.BeginRead(buffer, offset, count, callback, state);
        }

        public int EndRead(IAsyncResult asyncResult)
        {
            if (EncryptionEnabled)
                return _aesStream.EndRead(asyncResult);
            else
                return _stream.EndRead(asyncResult);
        }


        public void Purge()
        {
            var lenBytes = GetVarIntBytes(_buffer.Length);

            var tempBuff = new byte[_buffer.Length + lenBytes.Length];

            Buffer.BlockCopy(lenBytes, 0, tempBuff, 0, lenBytes.Length);
            Buffer.BlockCopy(_buffer, 0, tempBuff, lenBytes.Length, _buffer.Length);

            if (EncryptionEnabled)
                _aesStream.Write(tempBuff, 0, tempBuff.Length);
            else
                _stream.Write(tempBuff, 0, tempBuff.Length);

            _buffer = null;
        }


        public void Dispose()
        {
            if (_stream != null)
                _stream.Dispose();

            if (_aesStream != null)
                _aesStream.Dispose();

            _buffer = null;
        }
    }
}
