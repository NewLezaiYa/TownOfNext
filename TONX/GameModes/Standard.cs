using TONX.GameModes.Core;

namespace TONX.GameModes;

public sealed class Standard : GameModeBase
{
    public static readonly GameModeInfo ModeInfo =
        GameModeInfo.Create(
            typeof(Standard),
            () => new Standard(),
            CustomGameMode.Standard,
            100,
            null,
            "#ffffff"
        );
    public Standard() : base(ModeInfo)
    { }
}