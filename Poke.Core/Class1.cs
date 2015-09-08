using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Poke.Core
{
    public enum OpponentBattle
    {
        Opponent_1,
        Opponent_2,
        Opponent_3,
        Opponent_4,
        All,
        AllAndAttacker
    }

    public enum PokemonBattle
    {
        Pokemon_1,
        Pokemon_2,
        All
    }

    public enum MoveBattle
    {
        Move_1,
        Move_2,
        Move_3,
        Move_4
    }

    public enum PokemonToSwitch
    {
        Pokemon_1,
        Pokemon_2,
        Pokemon_3,
        Pokemon_4,
        Pokemon_5,
        Pokemon_6
    }

    public enum BattleMode
    {
        PlayerVsNature,
        PlayerVsNatureDual,

        PlayerVsBot,
        PlayerVsBotDual,

        PlayerVsPlayer,
        PlayerVsPlayerDual,

        TwoPlayersVsTwoPlayers
    }

    public enum BattleStatus
    {
        Correct,
        NotPossible,
        Corrected
    }
}
