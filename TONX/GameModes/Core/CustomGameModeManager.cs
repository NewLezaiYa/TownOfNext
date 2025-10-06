using TONX.Attributes;

namespace TONX.GameModes.Core;

public static class CustomGameModeManager
{
    public static Type[] AllModesClassType;
    public static readonly CustomGameMode[] AllModes = EnumHelper.GetAllValues<CustomGameMode>().Where(m => m is not CustomGameMode.All).ToArray();
    public static Dictionary<CustomGameMode, GameModeInfo> AllModesInfo = new(AllModes.Length);
    public static Dictionary<CustomGameMode, GameModeBase> AllModesClass = new(AllModes.Length);

    public static GameModeInfo GetModeInfo(this CustomGameMode mode) => AllModesInfo.ContainsKey(mode) ? AllModesInfo[mode] : null;
    public static GameModeBase GetModeClass(this CustomGameMode mode) => AllModesClass.ContainsKey(mode) ? AllModesClass[mode] : null;
    // ==初始化处理 ==
    [GameModuleInitializer]
    public static void Initialize()
    {
        Options.CurrentGameMode.GetModeClass()?.Init();
    }
    private static Dictionary<byte, long> LastSecondsUpdate = new();
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsInTask)
        {
            var now = Utils.GetTimeStamp();
            LastSecondsUpdate.TryAdd(player.PlayerId, 0);
            if (LastSecondsUpdate[player.PlayerId] != now)
            {
                Options.CurrentGameMode.GetModeClass()?.OnSecondsUpdate(player, now);
                LastSecondsUpdate[player.PlayerId] = now;
            }
            Options.CurrentGameMode.GetModeClass()?.OnFixedUpdate(player);
        }
    }
}


