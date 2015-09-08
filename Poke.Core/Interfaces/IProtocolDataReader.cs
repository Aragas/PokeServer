﻿using System;
using Org.BouncyCastle.Math;
using Poke.Core.Data;

namespace Poke.Core.Interfaces
{
    /// <summary>
    /// Object that reads data from IPacket.
    /// </summary>
    public interface IProtocolDataReader : IDisposable
    {
        String ReadString(Int32 length = 0);

        VarInt ReadVarInt();

        Boolean ReadBoolean();

        SByte ReadSByte();
        Byte ReadByte();

        Int16 ReadShort();
        UInt16 ReadUShort();

        Int32 ReadInt();
        UInt32 ReadUInt();

        Int64 ReadLong();
        UInt64 ReadULong();

        BigInteger ReadBigInteger();
        BigInteger ReadUBigInteger();

        Single ReadFloat();

        Double ReadDouble();


        String[] ReadStringArray(Int32 value);

        Int32[] ReadVarIntArray(Int32 value);

        Int32[] ReadIntArray(Int32 value);

        Byte[] ReadByteArray(Int32 value);
    }
}
