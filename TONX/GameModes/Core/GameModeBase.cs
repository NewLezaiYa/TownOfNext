using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using UnityEngine;

namespace TONX.GameModes.Core;

public abstract class GameModeBase
{
    public GameModeBase(GameModeInfo modeInfo)
    {
        CustomGameModeManager.AllModesClass.Add(modeInfo.ModeName, this);
    }
    public virtual void Init()
    { }
    /// <summary>
    /// 帧 Task 处理函数<br/>
    /// 不需要验证您的身份，因为调用前已经验证<br/>
    /// 请注意：全部模组端都会调用<br/>
    /// 如果您想在帧 Task 处理不是您自己时进行处理，请使用相同的参数将其实现为静态
    /// 并注册为 CustomRoleManager.OnFixedUpdateOthers
    /// </summary>
    /// <param name="player">目标玩家</param>
    public virtual void OnFixedUpdate(PlayerControl player)
    { }
    /// <summary>
    /// 秒 Task 处理函数<br/>
    /// 不需要验证您的身份，因为调用前已经验证<br/>
    /// 请注意：全部模组端都会调用<br/>
    /// </summary>
    /// <param name="player">目标玩家</param>
    /// <param name="now">当前10位时间戳</param>
    public virtual void OnSecondsUpdate(PlayerControl player, long now)
    { }

    /// <summary>
    /// 报告前检查调用的函数
    /// 与报告事件无关的玩家也会调用该函数
    /// </summary>
    /// <param name="reporter">报告者</param>
    /// <param name="target">被报告的玩家</param>
    /// <returns>false：取消报告</returns>
    public virtual bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => true;

    /// <summary>
    /// 报告时调用的函数
    /// 与报告事件无关的玩家也会调用该函数
    /// </summary>
    /// <param name="reporter">报告者</param>
    /// <param name="target">被报告的玩家</param>
    public virtual void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    { }
    // == 破坏相关处理 ==
    /// <summary>
    /// 当玩家造成破坏时调用
    /// 若禁止将无法关门
    /// </summary>
    /// <param name="systemType">破坏的设施类型</param>
    /// <returns>false：取消破坏</returns>
    public virtual bool OnInvokeSabotage(SystemTypes systemType) => true;

    /// <summary>
    /// 当有人破坏时调用
    /// </summary>
    /// <param name="player">造成破坏的玩家</param>
    /// <param name="systemType">造成破坏的设施</param>
    /// <returns>返回false取消破坏</returns>
    public virtual bool OnSabotage(PlayerControl player, SystemTypes systemType) => true;

    /// <summary>
    /// 游戏开始后会立刻调用该函数
    /// 默认为全体玩家调用
    /// </summary>
    public virtual void OnGameStart()
    { }
    public virtual void EditTaskText(TaskPanelBehaviour taskPanel, ref string AllText)
    { }
}
