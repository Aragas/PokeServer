using Poke.Core.Interfaces;

namespace Poke.Core.Data
{
    public class TrainerPetMoveMeta // 3 bits - opponents, 3 - pet // 2 - move
    {
        private readonly byte _meta;

        public byte Trainer { get; set; }   // 1-8
        public byte Pet { get; set; }       // 1-6
        public byte Move { get; set; }      // 1-4


        private TrainerPetMoveMeta(byte meta)
        {
            _meta = meta;
        }


        public static TrainerPetMoveMeta FromReader(IProtocolDataReader reader)
        {
            return new TrainerPetMoveMeta(reader.ReadByte());
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteByte(_meta);
        }
    }
}