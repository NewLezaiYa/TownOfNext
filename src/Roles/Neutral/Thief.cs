using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;

public sealed class Thief : RoleBase, IKiller, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Thief),
            player => new Thief(player),
            CustomRoles.Thief,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            50800,
            SetupOptionItem,
            "th|盗贼|小偷",
            "#8b0000",
            true,
            countType: CountTypes.Thief
        );

    public Thief(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => HasTask.False
    )
    {
        HasStolenAbility = false;
        TrialLimit = 0;
        ThiefDarkenDuration = OptionDarkenDuration.GetFloat();
        DarkenTimer = ThiefDarkenDuration;
        DarkenedPlayers = null;

        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionDarkenDuration;
    static OptionItem OptionTrialLimit;

    enum OptionName
    {
        ThiefDarkenDuration,
        ThiefTrialLimit,
    }

    // 盗贼状态
    private bool HasStolenAbility;
    private CustomRoles StolenRole;

    private static float KillCooldown;
    public static bool CanVent;
    private int TrialLimit;
    private float ThiefDarkenDuration;
    private float DarkenTimer;
    private PlayerControl[] DarkenedPlayers;
    private SystemTypes? DarkenedRoom = null;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 60f, 1f), 25f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, false, false);
        OptionDarkenDuration = FloatOptionItem.Create(RoleInfo, 13, OptionName.ThiefDarkenDuration, new(0.5f, 10f, 0.5f), 1f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTrialLimit = IntegerOptionItem.Create(RoleInfo, 14, OptionName.ThiefTrialLimit, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        HasStolenAbility = false;
        TrialLimit = OptionTrialLimit.GetInt();
        DarkenTimer = ThiefDarkenDuration;
        DarkenedPlayers = null;
    }

    public float CalculateKillCooldown() => KillCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(OptionHasImpostorVision.GetBool());
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => HasStolenAbility && OptionCanVent.GetBool() &&
        (StolenRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Engineer or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper);

    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        // 如果已经窃取过能力或者不是第一次击杀，则正常击杀
        if (HasStolenAbility || !info.DoKill || info.IsSuicide || info.IsAccident)
        {
            return true;
        }

        var target = info.AttemptTarget;
        var targetRole = target.GetCustomRole();

        // 检查目标是否是可以窃取能力的角色
        if (IsStealableRole(targetRole))
        {
            StealAbility(targetRole);
            Logger.Info($"盗贼 {Player.GetRealName()} 窃取了 {target.GetRealName()} ({targetRole}) 的能力", "Thief");

            // 显示提示信息
            Player.Notify(string.Format(GetString("ThiefStoleAbility"), Utils.GetRoleName(targetRole)));

            // 阻止击杀
            info.DoKill = false;
            // 重置击杀冷却时间
            Player.ResetKillCooldown();
            Player.SetKillCooldownV2();
            return false;
        }

        // 如果目标不可窃取能力，则正常击杀
        return true;
    }

    // 判断是否可以窃取该角色的能力
    private bool IsStealableRole(CustomRoles role)
    {
        // 不可窃取能力的角色列表
        var unstealableRoles = new HashSet<CustomRoles>
        {
            CustomRoles.Jester,
            CustomRoles.Innocent,
            CustomRoles.Gangster,
        };

        return !unstealableRoles.Contains(role);
    }

    // 窃取能力的实现
    private void StealAbility(CustomRoles targetRole)
    {
        HasStolenAbility = true;
        StolenRole = targetRole;

        switch (targetRole)
        {
            case CustomRoles.Judge:
                // 窃取法官的审判能力
                Player.Notify(string.Format(GetString("ThiefGotTrial"), TrialLimit));
                break;

            case CustomRoles.Stealth:
                // 窃取暗杀者的房间致盲能力
                Player.Notify(GetString("ThiefGotDarken"));
                break;

            default:
                // 对于基础内鬼职业，变成变形者或幻影师
                if (targetRole.IsImpostor() &&
                    targetRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Impostor)
                {
                    _ = IRandom.Instance.Next(0, 2) == 0 ? RoleTypes.Shapeshifter : RoleTypes.Phantom;
                }
                break;
        }
    }

    // 法官能力的审判实现
    public bool TrialMsg(PlayerControl pc, string msg, out bool spam)
    {
        spam = false;
        if (!HasStolenAbility || StolenRole != CustomRoles.Judge || !GameStates.IsInGame || pc == null)
            return false;

        if (!ChatCommand.OperateRoleCommand(ref msg, "sp|jj|tl|trial|审判|判|审", out int operate))
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("JudgeDead"), pc.PlayerId);
            return true;
        }

        if (operate == 1)
        {
            Utils.SendMessage(ChatCommand.GetFormatString(), pc.PlayerId);
            return true;
        }

        if (operate == 2)
        {
            spam = true;
            if (!AmongUsClient.Instance.AmHost) return true;

            if (!MsgToPlayer(msg, out PlayerControl target, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }

            if (!Trial(target, out var reason))
                Utils.SendMessage(reason, pc.PlayerId);
        }
        return true;
    }

    private bool Trial(PlayerControl target, out string reason, bool isUi = false)
    {
        reason = string.Empty;

        if (TrialLimit < 1)
        {
            reason = GetString("JudgeTrialMax");
            return false;
        }

        if (Is(target))
        {
            if (!isUi) Utils.SendMessage(GetString("LaughToWhoTrialSelf"), Player.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
            else Player.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoTrialSelf"));
            return false;
        }

        string Name = target.GetRealName();
        TrialLimit--;

        // 修复：简化LateTask嵌套，避免黑屏
        var state = PlayerState.GetByPlayerId(target.PlayerId);
        state.DeathReason = CustomDeathReason.Trialed;
        target.SetRealKiller(Player);

        // 使用更安全的方式处理死亡
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && target != null && !target.Data.IsDead)
            {
                target.RpcExileV2();
                target.Data.IsDead = true;
                target.Data.MarkDirty();

                // 延迟发送消息避免同步问题
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInGame)
                    {
                        Utils.SendMessage(string.Format(GetString("TrialKill"), Name), 255,
                            Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), GetString("TrialKillTitle")), false, true, Name);
                    }
                }, 0.3f, "Trial Message");
            }
        }, 0.1f, "Trial Execution");

        return true;
    }

    private static bool MsgToPlayer(string msg, out PlayerControl target, out string error)
    {
        error = string.Empty;
        target = Utils.MsgToPlayer(ref msg, out bool multiplePlayers);

        if (target == null)
        {
            error = multiplePlayers ? GetString("TrialMultipleColor") : GetString("TrialHelp");
            return false;
        }

        if (target.Data.IsDead)
        {
            error = GetString("TrialNull");
            return false;
        }

        return true;
    }

    private void RpcDarken(SystemTypes? roomType)
    {
        // 修复：添加网络状态检查
        if (!AmongUsClient.Instance.AmHost) return;

        DarkenedRoom = roomType;
        using var sender = CreateSender();
        sender.Writer.Write((byte?)roomType ?? byte.MaxValue);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        // 修复：添加异常处理
        try
        {
            var roomId = reader.ReadByte();
            DarkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Thief.ReceiveRPC error: {ex.Message}", "Thief");
            DarkenedRoom = null;
        }
    }

    private void ResetDarkenState()
    {
        // 修复：添加安全检查，避免在不适当的时候重置
        if (!GameStates.IsInGame) return;

        if (DarkenedPlayers != null)
        {
            foreach (var player in DarkenedPlayers)
            {
                if (player != null && !player.Data.IsDead)
                {
                    var state = PlayerState.GetByPlayerId(player.PlayerId);
                    if (state != null)
                    {
                        state.IsBlackOut = false;
                    }
                    player.MarkDirtySettings();
                }
            }
            DarkenedPlayers = null;
        }
        DarkenTimer = ThiefDarkenDuration;
        RpcDarken(null);

        // 修复：只在玩家是盗贼时通知角色
        if (Player != null && Player.IsAlive())
        {
            Utils.NotifyRoles(SpecifySeer: Player);
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        // 修复：添加安全检查，避免空引用
        if (!GameStates.IsInGame || Player == null || !Player.IsAlive()) return;

        // 修复：优化黑暗效果逻辑，减少不必要的计算
        if (HasStolenAbility && StolenRole == CustomRoles.Stealth && DarkenedRoom.HasValue)
        {
            DarkenTimer -= Time.fixedDeltaTime;
            if (DarkenTimer <= 0f)
            {
                ResetDarkenState();
            }
        }
    }
    public override void OnStartMeeting()
    {
        // 修复：添加安全检查
        if (!GameStates.IsMeeting) return;

        // 会议开始时重置隐匿者能力
        if (DarkenedPlayers != null)
        {
            ResetDarkenState();
        }

        // 修复：确保所有状态正确重置
        DarkenTimer = ThiefDarkenDuration;
        DarkenedRoom = null;
    }

    // 显示剩余能力次数
    public override string GetProgressText(bool comms = false)
    {
        if (!HasStolenAbility) return "";

        return StolenRole switch
        {
            CustomRoles.Judge => Utils.ColorString(TrialLimit > 0 ? Color.red : Color.gray, $"({TrialLimit})"),
            _ => ""
        };
    }

    // 显示能力状态
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != Player || seen != Player) return "";

        var suffix = "";
        if (HasStolenAbility)
        {
            // 如果是暗杀者能力且正在致盲
            if (StolenRole == CustomRoles.Stealth && DarkenedRoom.HasValue)
            {
                suffix += string.Format(GetString("StealthDarkened"),
                    DestroyableSingleton<TranslationController>.Instance.GetString(DarkenedRoom.Value));
            }
        }
        else
        {
            suffix = GetString("ThiefNoAbility");
        }

        return suffix;
    }

    // 处理聊天命令
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = TrialMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }

    // 会议按钮
    public string ButtonName { get; private set; } = "Judge";
    public bool ShouldShowButton() => HasStolenAbility && StolenRole == CustomRoles.Judge && Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();

    public void OnClickButton(PlayerControl target)
    {
        if (!Trial(target, out var reason, true))
            Player.ShowPopUp(reason);
    }
}