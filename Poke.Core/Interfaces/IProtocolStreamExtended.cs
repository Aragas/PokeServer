﻿namespace Poke.Core.Interfaces
{
    public interface IProtocolStreamExtended : IProtocolStream
    {
        bool EncryptionEnabled { get; }

        void InitializeEncryption(byte[] key);
    }
}
