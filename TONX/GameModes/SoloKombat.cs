using UnityEngine;
using TONX.GameModes.Core;
using TONX.Roles.GameMode;

namespace TONX.GameModes;

public sealed class SoloKombat : GameModeBase
{
    public static readonly GameModeInfo ModeInfo =
        GameModeInfo.Create(
            typeof(SoloKombat),
            () => new SoloKombat(),
            CustomGameMode.SoloKombat,
            101,
            SetupCustomOption,
            "#f55252"
        );
    public SoloKombat() : base(ModeInfo)
    { }
    public static int RoundTime;

    //Options
    public static OptionItem KB_GameTime;
    public static OptionItem KB_ATKCooldown;
    public static OptionItem KB_HPMax;
    public static OptionItem KB_ATK;
    public static OptionItem KB_RecoverAfterSecond;
    public static OptionItem KB_RecoverPerSecond;
    public static OptionItem KB_ResurrectionWaitingTime;
    public static OptionItem KB_KillBonusMultiplier;

    public static void SetupCustomOption()
    {
        TextOptionItem.Create(10_100_001, "MenuTitle.GameMode", TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal));
        KB_GameTime = IntegerOptionItem.Create(10_000_001, "KB_GameTime", new(30, 300, 5), 180, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);
        KB_ATKCooldown = FloatOptionItem.Create(10_000_002, "KB_ATKCooldown", new(1f, 10f, 0.1f), 1f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Seconds);
        KB_HPMax = FloatOptionItem.Create(10_000_003, "KB_HPMax", new(10f, 990f, 5f), 100f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Health);
        KB_ATK = FloatOptionItem.Create(10_000_004, "KB_ATK", new(1f, 100f, 1f), 8f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Health);
        KB_RecoverPerSecond = FloatOptionItem.Create(10_000_005, "KB_RecoverPerSecond", new(1f, 180f, 1f), 2f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Health);
        KB_RecoverAfterSecond = IntegerOptionItem.Create(10_000_006, "KB_RecoverAfterSecond", new(0, 60, 1), 8, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Seconds);
        KB_ResurrectionWaitingTime = IntegerOptionItem.Create(10_000_007, "KB_ResurrectionWaitingTime", new(5, 990, 1), 15, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Seconds);
        KB_KillBonusMultiplier = FloatOptionItem.Create(10_000_008, "KB_KillBonusMultiplier", new(0.25f, 5f, 0.25f), 1.25f, TabGroup.GameSettings, false)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(Utils.GetRoleColor(CustomRoles.KB_Normal))
            .SetValueFormat(OptionFormat.Multiplier);
    }
    public override void Init()
    {
        RoundTime = KB_GameTime.GetInt() + 8;
    }
    public override bool SelectCustomRoles(ref Dictionary<PlayerControl, CustomRoles> RoleResult)
    {
        RoleResult = new();
        foreach (var pc in Main.AllAlivePlayerControls)
            RoleResult.Add(pc, pc.PlayerId == 0 && Options.EnableGM.GetBool() ? CustomRoles.GM : CustomRoles.KB_Normal);
        return true;
    }
    public override bool ShouldAssignAddons() => false;
    public override string GetLobbyUpperTag() => $"<color=#f55252><size=1.7>{GetString("ModeSoloKombat")}</size></color>";
    private static long LastFixedUpdate;
    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask) return;

        if (LastFixedUpdate == Utils.GetTimeStamp()) return;
        LastFixedUpdate = Utils.GetTimeStamp();
        // 减少全局倒计时
        RoundTime--;
    }
    public override bool OnCloseDoors(SystemTypes door) => false;
    public override bool OnCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target) => false;
    public override void EditTaskText(TaskPanelBehaviour taskPanel, ref string AllText)
    {
        var lpc = PlayerControl.LocalPlayer;
        var kb_Normal = lpc.GetRoleClass() as KB_Normal;

        if (lpc.GetCustomRole() is CustomRoles.KB_Normal)
        {
            AllText += "\r\n";
            AllText += $"\r\n{GetString("PVP.ATK")}: {kb_Normal?.ATK}";
            AllText += $"\r\n{GetString("PVP.DF")}: {kb_Normal?.DF}";
            AllText += $"\r\n{GetString("PVP.RCO")}: {kb_Normal?.HPReco}";
        }
        AllText += "\r\n";

        Dictionary<byte, string> SummaryText = new();
        List<byte> AllPlayerIds = PlayerState.AllPlayerStates.Keys.Where(k => (Utils.GetPlayerById(k)?.Data ?? null) != null).ToList();
        foreach (var id in AllPlayerIds)
        {
            if (Utils.GetPlayerById(id).GetCustomRole() is CustomRoles.GM) continue;
            string name = Main.AllPlayerNames[id].RemoveHtmlTags().Replace("\r\n", string.Empty);
            string summary = $"{GetDisplayScore(id)}  {Utils.ColorString(Main.PlayerColors[id], name)}";
            if (GetDisplayScore(id).ToString().Trim() == "") continue;
            SummaryText[id] = summary;
        }

        List<(int, byte)> list = new();
        foreach (var id in AllPlayerIds) list.Add((GetRankOfScore(id), id));
        list.Sort();
        foreach (var id in list.Where(x => SummaryText.ContainsKey(x.Item2))) AllText += "\r\n" + SummaryText[id.Item2];

        AllText = $"<size=80%>{AllText}</size>";
    }
    public override (bool, bool, bool, float) GetSummaryTextContent() => (false, false, false, 6.5f);
    public override (string, Color, string, Color, AudioClip)? GetIntroFormat(CustomRoles role)
        => (
            Utils.GetRoleName(role),
            Utils.GetRoleColor(role),
            GetString("ModeSoloKombat"),
            ColorUtility.TryParseHtmlString("#f55252", out var c) ? c : new(255, 255, 255, 255),
            DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx
        );
    public override (string, float, Color, Color, string, Color)? GetOutroFormat(byte winnerId)
        => (
            Main.AllPlayerNames[winnerId] + GetString("Win"),
            5f,
            Main.PlayerColors[winnerId],
            new Color32(245, 82, 82, 255),
            $"<color=#f55252>{GetString("ModeSoloKombat")}</color>",
            Color.red
        );
    private static Dictionary<byte, int> KBScore = new();
    public static string GetDisplayScore(byte playerId)
    {
        int rank = GetRankOfScore(playerId);
        string score = KBScore.TryGetValue(playerId, out var s) ? $"{s}" : "Invalid";
        string text = string.Format(GetString("KBDisplayScore"), rank.ToString(), score);
        Color color = Utils.GetRoleColor(CustomRoles.KB_Normal);
        return Utils.ColorString(color, text);
    }
    public static int GetRankOfScore(byte playerId)
    {
        if (!GameStates.IsLobby)
        {
            foreach (var player in Main.AllPlayerControls)
            {
                var role = player.GetRoleClass() as KB_Normal;
                KBScore.TryAdd(player.PlayerId, role?.Score ?? -255);
                KBScore[player.PlayerId] = role?.Score ?? -255;
            }
        }
        try
        {
            int ms = KBScore[playerId];
            int rank = 1 + KBScore.Values.Where(x => x > ms).Count();
            rank += KBScore.Where(x => x.Value == ms).ToList().IndexOf(new(playerId, ms));
            return rank;
        }
        catch
        {
            return Main.AllPlayerControls.Count();
        }
    }
    public override bool AfterCheckForGameEnd(GameOverReason reason, ref GameEndPredicate predicate)
    {
        if (CustomWinnerHolder.WinnerIds.Count > 0 || CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
        {
            ShipStatus.Instance.enabled = false;
            GameEndChecker.StartEndGame(reason);
            predicate = null;
        }
        return true;
    }
    public override GameEndPredicate Predicate() => new SoloKombatGameEndPredicate();
    class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;
            if (CustomWinnerHolder.WinnerIds.Count > 0) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorsByKill;

            if (RoundTime > 0) return false;

            var list = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && GetRankOfScore(x.PlayerId) == 1);
            var winner = list.FirstOrDefault();
            if (winner != null) CustomWinnerHolder.WinnerIds = new() { winner.PlayerId };
            else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            Main.DoBlockNameChange = true;

            return true;
        }
    }
}