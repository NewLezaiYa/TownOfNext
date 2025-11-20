using AmongUs.GameOptions;
using Rewired;
using TONX.Modules;
using TONX.Roles.Core.Interfaces;
using static TONX.GuesserHelper;

namespace TONX.Roles.Neutral;
public sealed class Guesser : RoleBase, IKiller, IMeetingButton, IGuesser
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Guesser),
            player => new Guesser(player),
            CustomRoles.Guesser,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            52800,
            SetupOptionItem,
            "gs|賭怪|赌怪|赌"
        );
    public Guesser(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    public static OptionItem OptionGuessNums;
    public static OptionItem OptionCanGuessAddons;
    public static OptionItem OptionCanGuessVanilla;
    public static OptionItem OptionGuessNumsToWin;
    public static OptionItem OptionSuicideIfGuessWrong;
    public static OptionItem OptionForbidGuessIfWrongThisMeeting;
    enum OptionName
    {
        GuesserCanGuessTimes,
        GCanGuessAdt,
        GCanGuessVanilla,
        GGuessNumsToWin,
        GSuicideIfGuessWrong,
        GForbidGuessIfWrongThisMeeting
    }

    public int GuessLimit { get; set; }
    public string GuessMaxMsg { get; set; } = "GGuessMax";
    public bool CanGuessAddons => OptionCanGuessAddons.GetBool();
    public bool CanGuessVanilla => OptionCanGuessVanilla.GetBool();
    private byte Target;
    public bool HasWrongGuess;
    private int CorrectGuesses;
    private static void SetupOptionItem()
    {
        OptionGuessNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.GuesserCanGuessTimes, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanGuessAddons = BooleanOptionItem.Create(RoleInfo, 11, OptionName.GCanGuessAdt, false, false);
        OptionCanGuessVanilla = BooleanOptionItem.Create(RoleInfo, 12, OptionName.GCanGuessVanilla, true, false);
        OptionGuessNumsToWin = IntegerOptionItem.Create(RoleInfo, 13, OptionName.GGuessNumsToWin, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionSuicideIfGuessWrong = BooleanOptionItem.Create(RoleInfo, 14, OptionName.GSuicideIfGuessWrong, false, false);
        OptionForbidGuessIfWrongThisMeeting = BooleanOptionItem.Create(RoleInfo, 15, OptionName.GForbidGuessIfWrongThisMeeting, true, false);
    }
    public override void Add()
    {
        GuessLimit = OptionGuessNums.GetInt();
        Target = byte.MaxValue;
        HasWrongGuess = false;
        CorrectGuesses = 0;
    }
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (Player.IsAlive() && seen.IsAlive() && isForMeeting)
        {
            nameText = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Guesser), seen.PlayerId.ToString()) + " " + nameText;
        }
    }
    public override void OnStartMeeting()
    {
        HasWrongGuess = false;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = (info.AttemptKiller, info.AttemptTarget);
        if (killer == null || target == null) return false;
        Target = target.PlayerId;
        return false;
    }
    public override void NotifyOnMeetingStart(ref List<(string, byte, string)> msgToSend)
    {
        if (Target == byte.MaxValue || (Utils.GetPlayerById(Target)?.Data.IsDead ?? true)) return;
        List<CustomRoles> Suspects = new();
        void AddSuspectedRoles(List<CustomRoles> roles)
        {
            for (int i = 0; i < 3; i++)
            {
                if (roles.Count == 0) break;
                var role = roles[IRandom.Instance.Next(roles.Count)];
                Suspects.Add(role);
                roles.Remove(role);
            }
        }
        AddSuspectedRoles(CustomRolesHelper.AllRoles.Where(r => r.IsImpostor() && r.IsEnable()).ToList());
        AddSuspectedRoles(CustomRolesHelper.AllRoles.Where(r => r.IsCrewmate() && r.IsEnable()).ToList());
        AddSuspectedRoles(CustomRolesHelper.AllRoles.Where(r => r.IsNeutral() && r.IsEnable()).ToList());
        msgToSend.Add((
            string.Format(GetString("GusserSuspectRoles"), Suspects.Select(r => Utils.ColorString(Utils.GetRoleColor(r), Utils.GetRoleName(r))).ToList()),
            Player.PlayerId,
            "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>"
        ));
    }
    public override void AfterMeetingTasks()
    {
        Target = byte.MaxValue;
    }
    public string ButtonName { get; private set; } = "Target";
    public bool ShouldShowButton() => Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = GuesserMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }
    public bool OnClickButtonLocal(PlayerControl target)
    {
        ShowGuessPanel(target.PlayerId, MeetingHud.Instance);
        return false;
    }
    public bool OnCheckGuessing(PlayerControl guesser, PlayerControl target, CustomRoles role, ref string reason)
    {
        if (HasWrongGuess)
        {
            reason = GetString("GGuessForbidden");
            return false;
        }
        return true;
    }
    public void OnGuessing(PlayerControl guesser, PlayerControl target, CustomRoles role, ref bool guesserSuicide)
    {
        if (guesserSuicide) HasWrongGuess = true;
        else CorrectGuesses++;
        if (!OptionSuicideIfGuessWrong.GetBool()) guesserSuicide = false;
    }
    public void AfterGuessing(PlayerControl guesser)
    {
        if (CorrectGuesses >= OptionGuessNumsToWin.GetInt())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Guesser);
            CustomWinnerHolder.WinnerIds.Add(guesser.PlayerId);
        }
    }
}