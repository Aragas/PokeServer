using System;

namespace Poke.Core.Interfaces
{
    public interface IPacket
    {
        Byte ID { get; }
        IPacket ReadPacket(IProtocolDataReader reader);
        IPacket WritePacket(IProtocolStream stream);
    }
}