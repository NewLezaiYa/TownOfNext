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
        TrialLimit = 0;
        HasStolenAbility = false;
        HasUsedStealThisRound = false;
        StolenRole = CustomRoles.NotAssigned;

        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
    }

    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionTrialLimit;

    enum OptionName
    {
        ThiefTrialLimit,
    }

    // 盗贼状态
    private bool HasStolenAbility;
    private CustomRoles StolenRole;
    private bool HasUsedStealThisRound;

    private static float KillCooldown;
    public static bool CanVent;
    private int TrialLimit;

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 60f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, false, false);
        OptionTrialLimit = IntegerOptionItem.Create(RoleInfo, 14, OptionName.ThiefTrialLimit, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        HasStolenAbility = false;
        HasUsedStealThisRound = false;
        StolenRole = CustomRoles.NotAssigned;
        TrialLimit = OptionTrialLimit.GetInt();
    }

    public float CalculateKillCooldown() => KillCooldown;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(OptionHasImpostorVision.GetBool());
    public bool CanUseSabotageButton() => true;
    public bool CanUseImpostorVentButton() => HasStolenAbility && OptionCanVent.GetBool() &&
        (StolenRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Engineer or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper);

    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!info.DoKill || info.IsSuicide || info.IsAccident)
        {
            return true;
        }

        var target = info.AttemptTarget;
        var targetRole = target.GetCustomRole();

        // 检查本轮是否已经盗取过职业
        if (HasUsedStealThisRound)
        {
            Logger.Info($"盗贼 {Player.GetRealName()} 本轮已盗取过职业，直接击杀 {target.GetRealName()}", "Thief");
            return true;
        }

        // 检查目标是否是可以窃取能力的角色
        if (IsStealableRole(targetRole))
        {
            // 窃取能力
            StealAbility(targetRole);
            HasUsedStealThisRound = true;
            Logger.Info($"盗贼 {Player.GetRealName()} 窃取了 {target.GetRealName()} ({targetRole}) 的能力", "Thief");

            Player.Notify(string.Format(GetString("ThiefStoleAbility"), Utils.GetRoleName(targetRole)));

            info.DoKill = false;
            Player.ResetKillCooldown();
            Player.SetKillCooldownV2();
            return false;
        }

        return true;
    }

    private bool IsStealableRole(CustomRoles role)
    {
        return role == CustomRoles.Judge;
    }

    private void StealAbility(CustomRoles targetRole)
    {
        HasStolenAbility = true;
        StolenRole = targetRole;

        switch (targetRole)
        {
            case CustomRoles.Judge:
                TrialLimit = OptionTrialLimit.GetInt();
                Player.Notify(string.Format(GetString("ThiefGotTrial"), TrialLimit));
                break;

            default:
                if (targetRole.IsImpostor() &&
                    targetRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Impostor)
                {
                    Player.Notify(string.Format(GetString("ThiefGotShapeshift"), Utils.GetRoleName(targetRole)));
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

    public override void OnStartMeeting()
    {
        HasStolenAbility = false;
        HasUsedStealThisRound = false;
    }

    public override string GetProgressText(bool comms = false)
    {
        if (!HasStolenAbility) return "";

        return StolenRole switch
        {
            CustomRoles.Judge => Utils.ColorString(TrialLimit > 0 ? Color.red : Color.gray, $"({TrialLimit})"),
            _ => ""
        };
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != Player || seen != Player) return "";

        string suffix;
        if (HasStolenAbility)
        {
            suffix = string.Format(GetString("ThiefAbilityStatus"), Utils.GetRoleName(StolenRole));
        }
        else
        {
            suffix = GetString("ThiefNoAbility");
        }

        return suffix;
    }

    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = TrialMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }

    public string ButtonName { get; private set; } = "Judge";
    public bool ShouldShowButton() => HasStolenAbility && StolenRole == CustomRoles.Judge && Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();

    public void OnClickButton(PlayerControl target)
    {
        if (!Trial(target, out var reason, true))
            Player.ShowPopUp(reason);
    }
}