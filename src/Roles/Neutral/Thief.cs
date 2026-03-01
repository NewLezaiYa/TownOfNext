using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Neutral;

public sealed class Thief : RoleBase, IImpostor, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Thief),
            player => new Thief(player),
            CustomRoles.Thief,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            5600,
            SetupOptionItem,
            "th|盗贼|小偷",
            "#8b0000",
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter)
        );

    public Thief(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
    {
        HasStolenAbility = false;
        StolenRole = CustomRoles.Crewmate;
        TrialLimit = 0;
        ExcludeImpostors = OptionExcludeImpostors.GetBool();
        DarkenDuration = OptionDarkenDuration.GetFloat();
        DarkenTimer = DarkenDuration;
        DarkenedPlayers = null;
        CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionExcludeImpostors;
    static OptionItem OptionDarkenDuration;
    static OptionItem OptionTrialLimit;

    enum OptionName
    {
        KillCooldown,
        CanVent,
        ExcludeImpostors,
        DarkenDuration,
        TrialLimit,
        VoteStealCount
    }

    // 盗贼状态
    private bool HasStolenAbility;
    private CustomRoles StolenRole;

    private int TrialLimit;
    private bool ExcludeImpostors;
    private float DarkenDuration;
    private float DarkenTimer;
    private PlayerControl[] DarkenedPlayers;
    private SystemTypes? DarkenedRoom = null;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.KillCooldown, new(0f, 60f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, OptionName.CanVent, false, false);
        OptionExcludeImpostors = BooleanOptionItem.Create(RoleInfo, 12, OptionName.ExcludeImpostors, true, false);
        OptionDarkenDuration = FloatOptionItem.Create(RoleInfo, 13, OptionName.DarkenDuration, new(0.5f, 10f, 0.5f), 3f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionTrialLimit = IntegerOptionItem.Create(RoleInfo, 14, OptionName.TrialLimit, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        HasStolenAbility = false;
        StolenRole = CustomRoles.Crewmate;
        TrialLimit = OptionTrialLimit.GetInt();
        DarkenTimer = DarkenDuration;
        DarkenedPlayers = null;
    }

    public float CalculateKillCooldown() => OptionKillCooldown.GetFloat();
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
                // 窃取隐匿者的房间致盲能力
                Player.Notify(GetString("ThiefGotDarken"));
                break;

            default:
                // 对于基础内鬼职业，变成变形者或幻影师
                if (targetRole.IsImpostor() &&
                    targetRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Impostor)
                {
                    var newBaseRole = IRandom.Instance.Next(0, 2) == 0 ? RoleTypes.Shapeshifter : RoleTypes.Phantom;
                    // 这里只是记录，实际的基础职业变更需要在其他地方处理
                    Player.Notify(string.Format(GetString("ThiefGotShapeshift"),
                        newBaseRole == RoleTypes.Shapeshifter ? GetString("Shapeshifter") : GetString("Phantom")));
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

        _ = new LateTask(() =>
        {
            var state = PlayerState.GetByPlayerId(target.PlayerId);
            state.DeathReason = CustomDeathReason.Trialed;
            target.SetRealKiller(Player);
            target.RpcSuicideWithAnime();

            Utils.NotifyRoles(isForMeeting: true, NoCache: true);

            _ = new LateTask(() =>
            {
                Utils.SendMessage(string.Format(GetString("TrialKill"), Name), 255,
                    Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), GetString("TrialKillTitle")), false, true, Name);
            }, 0.6f, "Trial Kill");

        }, 0.2f, "Trial Kill");

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
        DarkenedRoom = roomType;
        using var sender = CreateSender();
        sender.Writer.Write((byte?)roomType ?? byte.MaxValue);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var roomId = reader.ReadByte();
        DarkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
    }

    private void ResetDarkenState()
    {
        if (DarkenedPlayers != null)
        {
            foreach (var player in DarkenedPlayers)
            {
                PlayerState.GetByPlayerId(player.PlayerId).IsBlackOut = false;
                player.MarkDirtySettings();
            }
            DarkenedPlayers = null;
        }
        DarkenTimer = DarkenDuration;
        RpcDarken(null);
        Utils.NotifyRoles(SpecifySeer: Player);
    }

    // 固定更新处理各种能力
    public static void OnFixedUpdateOthers(PlayerControl player)
    {
        if (player == null || !player.Is(CustomRoles.Thief)) return;

        var thief = player.GetRoleClass() as Thief;
        if (thief == null) return;

        // 处理隐匿者能力的定时器
        if (thief.DarkenedPlayers != null)
        {
            thief.DarkenTimer -= Time.fixedDeltaTime;
            if (thief.DarkenTimer <= 0)
            {
                thief.ResetDarkenState();
            }
        }
    }

    public override void OnFixedUpdate(PlayerControl player) { }
    public override void OnStartMeeting()
    {
        // 会议开始时重置隐匿者能力
        if (DarkenedPlayers != null)
        {
            ResetDarkenState();
        }
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
            suffix = string.Format(GetString("ThiefAbilityStatus"), Utils.GetRoleName(StolenRole));

            // 如果是隐匿者能力且正在致盲
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
    public  bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();

    public void OnClickButton(PlayerControl target)
    {
        if (!Trial(target, out var reason, true))
            Player.ShowPopUp(reason);
    }
}