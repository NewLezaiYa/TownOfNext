using AmongUs.GameOptions;
using UnityEngine;

namespace TONX.GameModes.Core;

public class GameModeInfo
{
    public Type ClassType;
    public CustomGameMode ModeName;
    public Func<GameModeBase> CreateInstance;
    public Color ModeColor;
    public string ModeColorCode;
    public int ConfigId;
    public OptionCreatorDelegate OptionCreator;

    private GameModeInfo(
        Type classType,
        Func<GameModeBase> createInstance,
        CustomGameMode modeName,
        int configId,
        OptionCreatorDelegate optionCreator,
        string colorCode
    )
    {
        ClassType = classType;
        CreateInstance = createInstance;
        ModeName = modeName;
        ConfigId = configId;
        OptionCreator = optionCreator;

        if (colorCode == "") colorCode = "#ffffff";
        ModeColorCode = colorCode;

        _ = ColorUtility.TryParseHtmlString(colorCode, out ModeColor);

        CustomGameModeManager.AllModesInfo.Add(modeName, this);
    }
    public static GameModeInfo Create(
        Type classType,
        Func<GameModeBase> createInstance,
        CustomGameMode modeName,
        int configId,
        OptionCreatorDelegate optionCreator,
        string colorCode = ""
    )
    {
        var modeInfo = new GameModeInfo(
                classType,
                createInstance,
                modeName,
                configId,
                optionCreator,
                colorCode
            );
        return modeInfo;
    }
    public delegate void OptionCreatorDelegate();
}