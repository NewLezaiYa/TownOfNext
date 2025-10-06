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
    /// <summary>
    /// 游戏开始初始化时调用
    /// </summary>
    public virtual void Init()
    { }
    /// <summary>
    /// 用于特殊模式分配主职业
    /// </summary>
    /// <param name="RoleResult">职业分配字典</param>
    /// <returns>返回false进行标准模式职业分配</returns>
    public virtual bool SelectCustomRoles(ref Dictionary<PlayerControl, CustomRoles> RoleResult) => false;
    /// <summary>
    /// 用于判断特殊模式是否分配附加职业
    /// </summary>
    /// <returns>返回false不分配附加职业</returns>
    public virtual bool ShouldAssignAddons() => true;
    /// <summary>
    /// 游戏结束的判断
    /// </summary>
    /// <returns>返回null则使用标准模式判断</returns>
    public virtual GameEndPredicate Predicate() => null;
    /// <summary>
    /// 判断游戏是否结束时调用
    /// </summary>
    /// <param name="reason">游戏结束原因</param>
    /// <param name="predicate">游戏结束判断，可进行修改</param>
    /// <returns>返回true则不进行标准模式游戏结束过程</returns>
    public virtual bool AfterCheckForGameEnd(GameOverReason reason, ref GameEndPredicate predicate) => false;
    /// <summary>
    /// 帧 Task 处理函数<br/>
    /// 不需要验证您的身份，因为调用前已经验证<br/>
    /// 请注意：全部模组端都会调用<br/>
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
    /// 当有人关门时调用
    /// </summary>
    /// <param name="door">关门的房间</param>
    /// <returns>返回false取消关门</returns>
    public virtual bool OnCloseDoors(SystemTypes door) => true;
    /// <summary>
    /// 设置任务栏文字时调用
    /// 用于修改任务栏文字
    /// </summary>
    /// <param name="taskPanel">任务栏实例</param>
    /// <param name="AllText">需要修改的文字</param>
    public virtual void EditTaskText(TaskPanelBehaviour taskPanel, ref string AllText)
    { }
    /// <summary>
    /// 获取大厅房主标签
    /// </summary>
    public virtual string GetLobbyUpperTag() => $"<color=#87cefa>{Main.PluginVersion}</color>";
    /// <summary>
    /// 复盘信息显示的内容
    /// (是否显示击杀数量, 是否显示生命状态, 是否显示击杀者, 职业名前的附加空格)
    /// </summary>
    public virtual (bool, bool, bool, float) GetSummaryTextContent() => (true, true, true, 0f);
    /// <summary>
    /// 修改开始界面的样式
    /// (阵营文字, 阵营文字颜色, 内鬼数量文字, 背景颜色, 音频)
    /// </summary>
    /// <param name="role">自己的职业</param>
    /// <returns>返回null则不进行修改</returns>
    public virtual (string, Color, string, Color, AudioClip)? GetIntroFormat(CustomRoles role) => null;
    /// <summary>
    /// 修改结束界面的样式
    /// (获胜阵营文字, 获胜阵营文字字体缩小量, 获胜阵营文字颜色, 背景颜色, 获胜文字, 获胜文字颜色)
    /// </summary>
    /// <param name="winnerId">获胜者玩家id</param>
    /// <returns>返回null则不进行修改</returns>
    public virtual (string, float, Color, Color, string, Color)? GetOutroFormat(byte winnerId) => null;
}
