using Poke.Core.Interfaces;

namespace Poke.Core.Data
{
    public class TrainerPetMeta // 3 bits - opponents, 3 - pet
    {
        private readonly byte _meta;

        public OpponentBattle Trainer { get; set; }
        public PokemonBattle Pet { get; set; }


        private TrainerPetMeta(byte meta)
        {
            _meta = meta;
        }


        public static TrainerPetMeta FromReader(IProtocolDataReader reader)
        {
            return new TrainerPetMeta(reader.ReadByte());
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteByte(_meta);
        }
    }
}