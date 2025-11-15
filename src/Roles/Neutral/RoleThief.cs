using AmongUs.GameOptions;
using Hazel;
using TONX.Roles.Core.Interfaces;

namespace TONX.Roles.Neutral;

public sealed class RoleThief : RoleBase, IKiller, IDeathReasonSeeable
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(RoleThief),
            player => new RoleThief(player),
            CustomRoles.RoleThief,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Neutral,
            51900,
            SetupOptionItem,
            "rt|连环交换|连环交换师",
            "#696969",
            true,
            introSound: () => GetIntroSound(RoleTypes.Shapeshifter),
            experimental: true
        );
    public RoleThief(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        TargetPlayer = byte.MaxValue;
    }
    private static OptionItem OptionSwapCooldown;
    enum OptionName
    {
        RoleThiefSkillCooldown,
    }
    private byte TargetPlayer;
    public static void SetupOptionItem()
    {
        OptionSwapCooldown = FloatOptionItem.Create(RoleInfo, 10, OptionName.RoleThiefSkillCooldown, new(2.5f, 180f, 2.5f), 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public float CalculateKillCooldown() => CanUseKillButton() ? OptionSwapCooldown.GetFloat() : 255f;
    public bool CanUseKillButton() => TargetPlayer == byte.MaxValue;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => false;
    public override void ApplyGameOptions(IGameOptions opt) => opt.SetVision(false);
    public bool OverrideKillButtonText(out string text)
    {
        text = GetString("RoleThiefKillButtonText");
        return true;
    }
    public bool OnCheckMurderAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (TargetPlayer == byte.MaxValue)
        {
            TargetPlayer = target.PlayerId;
            SendRPC();
        }
        return false;
    }
    public override void AfterMeetingTasks()
    {
        if (TargetPlayer == byte.MaxValue) return;
        var target = Utils.GetPlayerById(TargetPlayer);
        if (target == null || (target.Data?.IsDead ?? true)) return;
        var player = Player;

        player.RpcChangeRole(target.GetCustomRole());
        target.RpcChangeRole(CustomRoles.RoleThief);
        Logger.Info($"连环交换师{player?.Data?.PlayerName}与{target?.Data?.PlayerName}交换了职业", "RoleThief");
        Utils.NotifyRoles();
    }
    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(TargetPlayer);
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        TargetPlayer = reader.ReadByte();
    }
}
