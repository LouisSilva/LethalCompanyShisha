using GameNetcodeStuff;
using System.Runtime.CompilerServices;

namespace LethalCompanyShisha.Util;

internal static class PlayerUtil 
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(ulong playerClientId) 
    {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlayerControllerB GetPlayerFromClientId(int playerClientId) 
    {
        return StartOfRound.Instance.allPlayerScripts[playerClientId];
    }
    
    // Used so I dont mix up `PlayerControllerB.playerClientId` and `PlayerControllerB.actualClientId`
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetClientIdFromPlayer(PlayerControllerB player)
    {
        return player.playerClientId;
    }
    
    /// <summary>
    /// Determines whether the specified player is dead.
    /// </summary>
    /// <param name="player">The player to check if dead.</param>
    /// <returns>Returns <c>true</c> if the player is dead or not controlled; otherwise, <c>false</c>.</returns>
    internal static bool IsPlayerDead(PlayerControllerB player)
    {
        if (!player) return true;
        return player.isPlayerDead || !player.isPlayerControlled;
        
    }
}
