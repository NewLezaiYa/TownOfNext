using System.Text.RegularExpressions;
using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using TONX.Roles.Core.Interfaces;
using UnityEngine;

namespace TONX.Roles.Crewmate;
public sealed class Swapper : RoleBase, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Swapper),
            player => new Swapper(player),
            CustomRoles.Swapper,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            23200,
            SetupOptionItem,
            "swa|换票师|换票",
            "#863756"
        );
    public Swapper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        SwapLimit = OptionSwapLimit.GetInt();
    }

    private static OptionItem OptionSwapLimit;
    private static OptionItem OptionCanUseButton;
    enum OptionName
    {
        SwapperSwapLimit,
        SwapperCanUseButton
    }

    public int SwapLimit;
    public List<byte> Targets = new();

    private static void SetupOptionItem()
    {
        OptionSwapLimit = IntegerOptionItem.Create(RoleInfo, 10, OptionName.SwapperSwapLimit, new(1, 99, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanUseButton = BooleanOptionItem.Create(RoleInfo, 11, OptionName.SwapperCanUseButton, true, false);
    }
    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (Is(reporter) && target == null && !OptionCanUseButton.GetBool())
        {
            Logger.Info("因禁止换票师拍灯取消会议", "Swapper.OnCheckReportDeadBody");
            return false;
        }
        return true;
    }
    public override void OnStartMeeting()
    {
        Targets.Clear();
        SendRpc();
    }
    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (Player.IsAlive() && seen.IsAlive() && isForMeeting)
        {
            nameText = Utils.ColorString(RoleInfo.RoleColor, seen.PlayerId.ToString()) + " " + nameText;
        }
    }
    public string ButtonName { get; private set; } = "Swapper";
    public bool ShouldShowButton() => Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = SwapMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }
    public void OnClickButton(PlayerControl target)
    {
        if (!Swap(target, out var reason))
            Player.ShowPopUp(reason);
    }
    public void OnUpdateButton(MeetingHud meetingHud)
    {
        foreach (var pva in meetingHud.playerStates)
        {
            var btn = pva?.transform?.FindChild("Custom Meeting Button")?.gameObject;
            if (!btn) continue;
            if (Targets.Contains(pva.TargetPlayerId)) btn.GetComponent<SpriteRenderer>().color = Color.green;
            else btn.GetComponent<SpriteRenderer>().color = Targets.Count == 2 ? Color.gray : Color.white;
        }
    }
    private bool Swap(PlayerControl target, out string reason)
    {
        reason = string.Empty;

        if (Targets.Contains(target.PlayerId))
        {
            reason = GetString("TargetAlreadySwapped");
            return false;
        }
        if (Targets.Count == 2)
        {
            reason = GetString("SwapUsed");
            return false;
        }
        if (SwapLimit < 1)
        {
            reason = GetString("SwapperSwapMax");
            return false;
        }

        string Name = target.GetRealName();

        Targets.Add(target.PlayerId);
        if (Targets.Count == 2)
        {
            SendRpc();
            SwapLimit--;
            Logger.Info($"{Player.GetNameWithRole()} => Swap {Utils.GetPlayerById(Targets[0])?.Data?.PlayerName} with {Utils.GetPlayerById(Targets[1])?.Data?.PlayerName}", "Swapper");
        }

        _ = new LateTask (() =>
        {
            Utils.SendMessage(
                string.Format(GetString("SwapSkill"), Name),
                Player.PlayerId,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Swapper), GetString("SwapVoteTitle")));
        }, 0.8f, "Swap Skill");

        return true;
    }
    public bool SwapMsg(PlayerControl pc, string msg, out bool spam)
    {
        spam = false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.Swapper)) return false;

        int operate; // 1:ID 2:交换
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (GuesserHelper.MatchCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (GuesserHelper.MatchCommand(ref msg, "sw|swa|swap|换票|换", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("SwapperDead"), pc.PlayerId);
            return true;
        }

        if (operate == 1)
        {
            Utils.SendMessage(GuesserHelper.GetFormatString(), pc.PlayerId);
            return true;
        }
        if (operate == 2)
        {
            spam = true;
            if (!AmongUsClient.Instance.AmHost) return true;

            if (!MsgToPlayer(msg, out byte targetId, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }

            var target = Utils.GetPlayerById(targetId);
            if (!Swap(target, out var reason))
                Utils.SendMessage(reason, pc.PlayerId);
        }
        return true;
    }
    private static bool MsgToPlayer(string msg, out byte id, out string error)
    {
        error = string.Empty;
        id = GuesserHelper.GetPlayerIdFromMsg(ref msg, ref error, "SwapNull", "SwapMultipleColor");

        //判断选择的玩家是否合理
        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
        {
            error = GetString("SwapNull");
            return false;
        }
        return true;
    }
    private void SendRpc()
    {
        using var sender = CreateSender();
        sender.Writer.Write(Targets.Count);
        foreach (var target in Targets) sender.Writer.Write(target);
        SetTargets();
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        Targets = new();
        var num = reader.ReadInt32();
        for (var i = 0; i < num; i++) Targets.Add(reader.ReadByte());
        SetTargets();
    }
    private void SetTargets()
    {
        if (Targets.Count == 2) MeetingVoteManager.Instance?.SetSwappedPlayers(Targets[0], Targets[1], true);
    }
}