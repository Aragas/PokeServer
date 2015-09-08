﻿using System.Security.Cryptography;
using Poke.Core.Interfaces;

namespace PokeServer.Packets.Joining
{
    public struct EncryptionRequestPacket : IPacket
    {
        public string ServerId;
        public byte[] PublicKey;
        public byte[] SharedKey;
        public byte[] VerificationToken;

        public byte ID { get { return 0x01; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            ServerId = reader.ReadString();
            var pkLength = reader.ReadVarInt();
            PublicKey = reader.ReadByteArray(pkLength);
            var vtLength = reader.ReadVarInt();
            VerificationToken = reader.ReadByteArray(vtLength);

            SharedKey = new byte[16];

            var random = RandomNumberGenerator.Create();
            random.GetBytes(SharedKey);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteString(ServerId);
            stream.WriteVarInt(PublicKey.Length);
            stream.WriteByteArray(PublicKey);
            stream.WriteVarInt(VerificationToken.Length);
            stream.WriteByteArray(VerificationToken);
            stream.Purge();

            return this;
        }
    }
}
