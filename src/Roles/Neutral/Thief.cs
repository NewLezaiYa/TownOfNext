using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using TONX.Roles.Core.Interfaces;
using TONX.Roles.Crewmate;
using TONX.Roles.Impostor;
using UnityEngine;
using static TONX.GuesserHelper;

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
            "th|盗贼 | 小偷",
            "#8b0000",
            true,
            countType: CountTypes.Thief
        );

    public Thief(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.False)
    {
        ResetThiefState();
    }

    // ========== 选项配置 ==========
    static OptionItem OptionKillCooldown;
    static OptionItem OptionCanVent;
    static OptionItem OptionHasImpostorVision;
    static OptionItem OptionTrialLimit;

    enum OptionName
    {
        ThiefTrialLimit,
    }

    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, new(0f, 60f, 1f), 15f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, false, false);
        OptionHasImpostorVision = BooleanOptionItem.Create(RoleInfo, 12, GeneralOption.ImpostorVision, false, false);
        OptionTrialLimit = IntegerOptionItem.Create(RoleInfo, 14, OptionName.ThiefTrialLimit, new(1, 10, 1), 1, false)
            .SetValueFormat(OptionFormat.Times);
    }

    // ========== 基础属性 ==========
    private static float KillCooldown;
    public static bool CanVent;

    // ========== 盗贼状态 ==========
    private bool HasStolenAbility;
    private CustomRoles StolenRole;
    private bool HasUsedStealThisRound;
    private HashSet<byte> StolenTargetIds = new();

    // ========== 窃取职业的状态变量 ==========
    // Judge/Justice
    private int TrialLimit;
    // Guesser
    private int GuessLimit;
    // Vampire
    private Dictionary<byte, float> BittenPlayers = new();
    private float KillDelay;
    // Butcher
    private List<byte> ButcherKilledPlayers = new();
    // Swooper
    private long InvisTime = -1;
    private long LastTime = -1;
    private int VentedId = -1;
    // Mayor
    private int LeftButtonCount;
    // Veteran
    private int SkillLimit;
    private long ProtectStartTime = 0;
    // Swapper
    private int SwapLimit;
    private List<byte> Targets = new();
    // Justice
    private List<byte> SelectedPlayers = new();

    public override void Add()
    {
        ResetThiefState();
    }

    private void ResetThiefState()
    {
        HasStolenAbility = false;
        HasUsedStealThisRound = false;
        StolenRole = CustomRoles.NotAssigned;
        StolenTargetIds.Clear();

        TrialLimit = OptionTrialLimit.GetInt();
        GuessLimit = 0;

        BittenPlayers.Clear();
        KillDelay = 0f;
        ButcherKilledPlayers.Clear();
        InvisTime = -1;
        LastTime = -1;
        VentedId = -1;
        LeftButtonCount = 0;
        SkillLimit = 0;
        ProtectStartTime = 0;
        SwapLimit = 0;
        Targets.Clear();
        SelectedPlayers.Clear();

        KillCooldown = OptionKillCooldown.GetFloat();
    }

    // ========== IKiller 接口实现 ==========
    public float CalculateKillCooldown()
    {
        if (!HasStolenAbility) return KillCooldown;

        return StolenRole switch
        {
            CustomRoles.Veteran when ProtectStartTime == 0 => Options.DefaultKillCooldown,
            _ => KillCooldown
        };
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptionHasImpostorVision.GetBool());

        if (!HasStolenAbility) return;

        switch (StolenRole)
        {
            case CustomRoles.Concealer:
                AURoleOptions.ShapeshifterCooldown = GetStaticOption<float>(typeof(Concealer), "OptionShapeshiftCooldown");
                AURoleOptions.ShapeshifterDuration = GetStaticOption<float>(typeof(Concealer), "OptionShapeshiftDuration");
                break;

            case CustomRoles.Mayor:
            case CustomRoles.Veteran:
                AURoleOptions.EngineerCooldown = LeftButtonCount <= 0 ? 255f : opt.GetInt(Int32OptionNames.EmergencyCooldown);
                AURoleOptions.EngineerInVentMaxTime = 1;
                break;
        }
    }

    public bool CanUseSabotageButton() => true;

    public bool CanUseImpostorVentButton()
    {
        if (!HasStolenAbility || !OptionCanVent.GetBool()) return false;

        return StolenRole switch
        {
            _ => StolenRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Engineer or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper
        };
    }

    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!info.DoKill || info.IsSuicide || info.IsAccident) return true;

        var target = info.AttemptTarget;
        var targetRole = target.GetCustomRole();

        // 已窃取过职业，执行特殊击杀逻辑
        if (HasUsedStealThisRound)
        {
            return HandleNormalKill(info, target, targetRole);
        }

        // 检查是否已窃取过该目标
        if (StolenTargetIds.Contains(target.PlayerId))
        {
            Player.Notify(GetString("ThiefAlreadyStolen"));
            return true;
        }

        // 尝试窃取能力
        if (IsStealableRole(targetRole))
        {
            return TryStealAbility(info, target, targetRole);
        }

        return true;
    }

    private bool HandleNormalKill(MurderInfo info, PlayerControl target, CustomRoles targetRole)
    {
        Logger.Info($"盗贼 {Player.GetRealName()} 本轮已盗取过职业，直接击杀 {target.GetRealName()}", "Thief");

        // 吸血鬼特殊处理
        if (StolenRole == CustomRoles.Vampire && !target.Is(CustomRoles.Bait) && !info.IsFakeSuicide)
        {
            if (!BittenPlayers.ContainsKey(target.PlayerId))
            {
                Player.SetKillCooldownV2();
                Player.RPCPlayCustomSound("Bite");
                BittenPlayers.Add(target.PlayerId, 0f);
                info.DoKill = false;
                return false;
            }
        }

        return true;
    }

    private bool TryStealAbility(MurderInfo info, PlayerControl target, CustomRoles targetRole)
    {
        StealAbility(target, targetRole);
        HasUsedStealThisRound = true;
        StolenTargetIds.Add(target.PlayerId);

        Logger.Info($"盗贼 {Player.GetRealName()} 窃取了 {target.GetRealName()} ({targetRole}) 的能力", "Thief");
        Player.Notify(string.Format(GetString("ThiefStoleAbility"), Utils.GetRoleName(targetRole)));

        info.DoKill = false;
        Player.ResetKillCooldown();
        Player.SetKillCooldownV2();
        return false;
    }

    public void BeforeMurderPlayerAsKiller(MurderInfo info)
    {
        if (!HasStolenAbility) return;

        var (killer, target) = info.AttemptTuple;

        switch (StolenRole)
        {
            case CustomRoles.Scavenger:
                HandleScavengerKill(killer, target, info);
                break;

            case CustomRoles.Butcher:
                HandleButcherKill(killer, target, info);
                break;

            case CustomRoles.Swooper:
                HandleSwooperKill(killer, target, info);
                break;
        }
    }

    private void HandleScavengerKill(PlayerControl killer, PlayerControl target, MurderInfo info)
    {
        if (!info.IsSuicide)
        {
            killer.RpcSnapToForced(target.GetTruePosition());
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            target.RpcSnapToForced(Utils.GetBlackRoomPS());
            target.SetRealKiller(killer);
            target.RpcMurderPlayerV2(target);
            killer.SetKillCooldownV2();
            NameNotifyManager.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scavenger), GetString("KilledByScavenger")));
            info.DoKill = false;
        }
    }

    private void HandleButcherKill(PlayerControl killer, PlayerControl target, MurderInfo info)
    {
        if (info.IsSuicide) return;

        info.DoKill = false;
        if (ButcherKilledPlayers.Contains(target.PlayerId)) return;

        PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Dismembered;
        _ = new LateTask(() =>
        {
            if (!ButcherKilledPlayers.Contains(target.PlayerId)) ButcherKilledPlayers.Add(target.PlayerId);
            var ops = target.GetTruePosition();
            var rd = IRandom.Instance;
            for (int i = 0; i < 20; i++)
            {
                Vector2 location = new(ops.x + ((float)(rd.Next(0, 201) - 100) / 100), ops.y + ((float)(rd.Next(0, 201) - 100) / 100));
                location += new Vector2(0, 0.3636f);
                target.NetTransform.SnapTo(location);
                killer.RpcMurderPlayerV2(target);
            }
            target.NetTransform.SnapTo(ops);
        }, 0.05f, "Thief-Butcher Murder");
    }

    private void HandleSwooperKill(PlayerControl killer, PlayerControl target, MurderInfo info)
    {
        if (IsInvis())
        {
            Utils.TP(killer.NetTransform, target.GetTruePosition());
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            killer.SetKillCooldownV2();
            target.SetRealKiller(killer);
            target.RpcMurderPlayerV2(target);
            info.DoKill = false;
        }
    }

    // ========== FixedUpdate 更新 ==========
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !HasStolenAbility) return;

        UpdateVampire();
        UpdateVeteran();
    }

    private void UpdateVampire()
    {
        if (StolenRole != CustomRoles.Vampire) return;

        foreach (var (targetId, timer) in BittenPlayers.ToArray())
        {
            if (timer >= KillDelay)
            {
                var target = Utils.GetPlayerById(targetId);
                KillBitten(target);
                BittenPlayers.Remove(targetId);
            }
            else
            {
                BittenPlayers[targetId] += Time.fixedDeltaTime;
            }
        }
    }

    private void UpdateVeteran()
    {
        if (StolenRole != CustomRoles.Veteran || ProtectStartTime == 0) return;

        if (ProtectStartTime + (long)GetStaticOption<float>(typeof(Veteran), "OptionSkillDuration") < Utils.GetTimeStamp())
        {
            ProtectStartTime = 0;
            Player.RpcProtectedMurderPlayer();
            Player.SyncSettings();
            Player.RpcResetAbilityCooldown();
            Player.Notify(string.Format(GetString("VeteranOffGuard"), SkillLimit));
        }
    }

    // ========== 会议相关 ==========
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (StolenRole == CustomRoles.Vampire)
        {
            foreach (var targetId in BittenPlayers.Keys)
            {
                var bittenTarget = Utils.GetPlayerById(targetId);
                KillBitten(bittenTarget, true);
            }
            BittenPlayers.Clear();
        }
    }

    public override void OnStartMeeting()
    {
        HasUsedStealThisRound = false;

        if (StolenRole == CustomRoles.Swapper) Targets.Clear();
        else if (StolenRole == CustomRoles.Justice) SelectedPlayers.Clear();
    }

    // ========== 能力窃取 ==========
    private bool IsStealableRole(CustomRoles role)
    {
        return role is CustomRoles.Judge or CustomRoles.NiceGuesser or CustomRoles.EvilGuesser
            or CustomRoles.Vampire or CustomRoles.Scavenger or CustomRoles.Concealer
            or CustomRoles.Butcher or CustomRoles.Swooper or CustomRoles.Mayor
            or CustomRoles.Veteran or CustomRoles.Swapper or CustomRoles.Justice;
    }

    private void StealAbility(PlayerControl target, CustomRoles targetRole)
    {
        HasStolenAbility = true;
        StolenRole = targetRole;

        switch (targetRole)
        {
            case CustomRoles.Judge:
                TrialLimit = OptionTrialLimit.GetInt();
                Player.Notify(string.Format(GetString("ThiefGotTrial"), TrialLimit));
                break;

            case CustomRoles.NiceGuesser:
            case CustomRoles.EvilGuesser:
                GuessLimit = GetGuessLimitForRole(targetRole);
                Player.Notify(string.Format(GetString("ThiefGotGuesser"), Utils.GetRoleName(targetRole), GuessLimit));
                break;

            case CustomRoles.Vampire:
                KillDelay = GetStaticOption<float>(typeof(Vampire), "OptionKillDelay");
                Player.Notify(string.Format(GetString("ThiefGotVampire"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Scavenger:
                Player.Notify(string.Format(GetString("ThiefGotScavenger"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Concealer:
                Player.Notify(string.Format(GetString("ThiefGotConcealer"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Butcher:
                ButcherKilledPlayers = new();
                Player.Notify(string.Format(GetString("ThiefGotButcher"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Swooper:
                InvisTime = -1;
                LastTime = -1;
                VentedId = -1;
                Player.Notify(string.Format(GetString("ThiefGotSwooper"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Mayor:
                LeftButtonCount = GetStaticField<int>(typeof(Mayor), "NumOfUseButton");
                Player.Notify(string.Format(GetString("ThiefGotMayor"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Veteran:
                SkillLimit = GetStaticOption<int>(typeof(Veteran), "OptionSkillNums");
                ProtectStartTime = 0;
                Player.Notify(string.Format(GetString("ThiefGotVeteran"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Swapper:
                SwapLimit = GetStaticOption<int>(typeof(Swapper), "OptionSwapLimit");
                Targets = new();
                Player.Notify(string.Format(GetString("ThiefGotSwapper"), Utils.GetRoleName(targetRole)));
                break;

            case CustomRoles.Justice:
                TrialLimit = OptionTrialLimit.GetInt();
                SelectedPlayers = new();
                Player.Notify(string.Format(GetString("ThiefGotJustice"), Utils.GetRoleName(targetRole), TrialLimit));
                break;

            default:
                var roleName = Utils.GetRoleName(targetRole);
                if (targetRole.IsImpostor() && targetRole.GetRoleInfo()?.BaseRoleType.Invoke() is RoleTypes.Impostor or RoleTypes.Shapeshifter)
                {
                    Player.Notify(string.Format(GetString("ThiefGotShapeshift"), roleName));
                }
                else
                {
                    Player.Notify(string.Format(GetString("ThiefGotOther"), roleName));
                }
                break;
        }
    }

    private int GetGuessLimitForRole(CustomRoles role)
    {
        return role switch
        {
            CustomRoles.NiceGuesser => GetStaticOption<int>(typeof(NiceGuesser), "OptionGuessNums"),
            CustomRoles.EvilGuesser => GetStaticOption<int>(typeof(EvilGuesser), "OptionGuessNums"),
            _ => 0
        };
    }

    private void KillBitten(PlayerControl target, bool isButton = false)
    {
        if (target.IsAlive())
        {
            PlayerState.GetByPlayerId(target.PlayerId).DeathReason = CustomDeathReason.Bite;
            target.SetRealKiller(Player);
            CustomRoleManager.OnCheckMurder(Player, target, target, target);
            Logger.Info($"盗贼伪装的吸血鬼咬死了 {target.name}", "Thief-Vampire.KillBitten");
            if (!isButton && Player.IsAlive())
            {
                RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
            }
        }
    }

    // ========== Swooper 相关 ==========
    private bool IsInvis() => InvisTime != -1;

    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (!HasStolenAbility || StolenRole != CustomRoles.Swooper) return base.OnEnterVent(physics, ventId);

        var now = Utils.GetTimeStamp();
        if (IsInvis())
        {
            LastTime = now;
            InvisTime = -1;
            NameNotifyManager.Notify(Player, GetString("SwooperInvisStateOut"));
            return false;
        }

        _ = new LateTask(() =>
        {
            if (LastTime == -1 || LastTime + (long)GetStaticOption<float>(typeof(Swooper), "SwooperCooldown") < now)
            {
                VentedId = ventId;
                InvisTime = now;
                NameNotifyManager.Notify(Player, GetString("SwooperInvisState"), GetStaticOption<float>(typeof(Swooper), "SwooperDuration"));
            }
            else
            {
                physics.RpcBootFromVent(ventId);
                NameNotifyManager.Notify(Player, GetString("SwooperInvisInCooldown"));
            }
        }, 0.5f, "Thief-Swooper Vent");

        return true;
    }

    // ========== Mayor/Veteran 相关 ==========
    public override int OverrideAbilityButtonUsesRemaining()
    {
        if (!HasStolenAbility) return base.OverrideAbilityButtonUsesRemaining();

        return StolenRole switch
        {
            CustomRoles.Mayor => LeftButtonCount,
            CustomRoles.Veteran => SkillLimit,
            _ => base.OverrideAbilityButtonUsesRemaining()
        };
    }

    // ========== 文本显示 ==========
    public override string GetProgressText(bool comms = false)
    {
        if (!HasStolenAbility) return "";

        return StolenRole switch
        {
            CustomRoles.Judge => Utils.ColorString(TrialLimit > 0 ? Color.red : Color.gray, $"({TrialLimit})"),
            CustomRoles.NiceGuesser or CustomRoles.EvilGuesser => Utils.ColorString(GuessLimit > 0 ? Color.yellow : Color.gray, $"({GuessLimit})"),
            CustomRoles.Mayor => Utils.ColorString(LeftButtonCount > 0 ? Color.green : Color.gray, $"({LeftButtonCount})"),
            CustomRoles.Veteran => Utils.ColorString(SkillLimit > 0 ? Color.blue : Color.gray, $"({SkillLimit})"),
            CustomRoles.Swapper => Utils.ColorString(SwapLimit > 0 ? Color.magenta : Color.gray, $"({SwapLimit})"),
            _ => ""
        };
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != Player || seen != Player) return "";

        string suffix = HasStolenAbility
            ? string.Format(GetString("ThiefAbilityStatus"), Utils.GetRoleName(StolenRole))
            : GetString("ThiefNoAbility");

        return suffix;
    }

    // ========== 消息处理 ==========
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        if ((StolenRole == CustomRoles.Judge || StolenRole == CustomRoles.Justice) && TrialMsg(Player, msg, out bool spam))
        {
            recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
            return true;
        }

        if ((StolenRole == CustomRoles.NiceGuesser || StolenRole == CustomRoles.EvilGuesser) && GuesserMsg(Player, msg, out spam))
        {
            recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
            return true;
        }

        recallMode = MsgRecallMode.None;
        return false;
    }

    // ========== Judge/Justice 审判 ==========
    public bool TrialMsg(PlayerControl pc, string msg, out bool spam)
    {
        spam = false;
        if (!HasStolenAbility || (StolenRole != CustomRoles.Judge && StolenRole != CustomRoles.Justice) || !GameStates.IsInGame || pc == null)
            return false;

        if (!ChatCommand.OperateRoleCommand(ref msg, "sp|jj|tl|trial|审判 | 判 | 审", out int operate))
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

    // ========== Guesser 猜测 ==========
    public bool GuesserMsg(PlayerControl pc, string msg, out bool spam)
    {
        spam = false;
        if (!HasStolenAbility || (StolenRole != CustomRoles.NiceGuesser && StolenRole != CustomRoles.EvilGuesser) || !GameStates.IsInGame || pc == null)
            return false;

        if (!ChatCommand.OperateRoleCommand(ref msg, "shoot|guess|bet|st|gs|bt|猜 | 赌", out int operate))
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("GuessDead"), pc.PlayerId);
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

            if (!MsgToPlayerAndRole(msg, out PlayerControl target, out CustomRoles role, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }

            if (!Guess(pc, target, role, out var reason))
                Utils.SendMessage(reason, pc.PlayerId);
        }
        return true;
    }

    private static bool MsgToPlayerAndRole(string msg, out PlayerControl target, out CustomRoles role, out string error)
    {
        error = string.Empty;
        role = new();

        target = Utils.MsgToPlayer(ref msg, out bool multiplePlayers);
        if (target == null)
        {
            error = multiplePlayers ? GetString("GuessMultipleColor") : GetString("GuessHelp");
            return false;
        }
        if (target.Data.IsDead)
        {
            error = GetString("GuessNull");
            return false;
        }
        if (Justice.UnableToBeTargetedInJusticeMeeting(target))
        {
            error = GetString("JusticeMeetingBanAbility");
            return false;
        }
        if (!ChatCommand.GetRoleByInputName(msg, out role, true))
        {
            error = GetString("GuessHelp");
            return false;
        }

        return true;
    }

    // ========== 会议按钮 ==========
    public string ButtonName { get; private set; } = "Judge";
    public bool ShouldShowButton() => HasStolenAbility && (StolenRole == CustomRoles.Judge || StolenRole == CustomRoles.Justice) && Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();

    public void OnClickButton(PlayerControl target)
    {
        if (!Trial(target, out var reason, true))
            Player.ShowPopUp(reason);
    }

    public bool OnClickButtonLocal(PlayerControl target)
    {
        if (StolenRole == CustomRoles.NiceGuesser || StolenRole == CustomRoles.EvilGuesser)
        {
            ShowGuessPanel(target.PlayerId, MeetingHud.Instance);
            return false;
        }
        return true;
    }

    // ========== 辅助方法 ==========
    private static T GetStaticOption<T>(Type roleType, string optionName)
    {
        var field = roleType.GetField(optionName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (field == null) return default;

        if (field.GetValue(null) is not OptionItem optionItem) return default;

        return (T)Convert.ChangeType(optionItem.GetValue(), typeof(T));
    }

    private static T GetStaticField<T>(Type roleType, string fieldName)
    {
        var field = roleType.GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (field == null) return default;
        return (T)field.GetValue(null);
    }
}