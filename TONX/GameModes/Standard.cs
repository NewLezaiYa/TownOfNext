using System.Text;
using UnityEngine;
using TONX.GameModes.Core;

namespace TONX.GameModes;

public sealed class Standard : GameModeBase
{
    public static readonly GameModeInfo ModeInfo =
        GameModeInfo.Create(
            typeof(Standard),
            () => new Standard(),
            CustomGameMode.Standard,
            100,
            null,
            "#ffffff"
        );
    public Standard() : base(ModeInfo)
    { }
    public override void EditTaskText(TaskPanelBehaviour taskPanel, ref string AllText)
    {
        var taskText = taskPanel.taskText.text;
        var lines = taskText.Split("\r\n</color>\n")[0].Split("\r\n\n")[0].Split("\r\n");
        StringBuilder sb = new();
        foreach (var eachLine in lines)
        {
            var line = eachLine.Trim();
            if ((line.StartsWith("<color=#FF1919FF>") || line.StartsWith("<color=#FF0000FF>")) && sb.Length < 1 && !line.Contains('(')) continue;
            sb.Append(line + "\r\n");
        }
        if (sb.Length > 1)
        {
            var text = sb.ToString().TrimEnd('\n').TrimEnd('\r');
            if (!Utils.HasTasks(PlayerControl.LocalPlayer.Data, false) && sb.ToString().Count(s => s == '\n') >= 2)
                text = $"{Utils.ColorString(new Color32(255, 20, 147, byte.MaxValue), GetString("FakeTask"))}\r\n{text}";
            AllText += $"\r\n\r\n<size=85%>{text}</size>";
        }

        if (MeetingStates.FirstMeeting)
            AllText += $"\r\n\r\n</color><size=70%>{GetString("PressF1ShowRoleDescription")}</size>";
    }
}