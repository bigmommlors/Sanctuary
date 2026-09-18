using System;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Temporary local-debug escape from sg_bandit_hideout when the Exit UI never appears.
/// Invoked as !leavebandit (chat command prefix). Gateway also accepts /leavebandit.
/// </summary>
public class LeaveBanditChatCommand : IChatCommand
{
    /// <summary>
    /// Gateway registers this so the Game-layer chat command can reach the shared Bandit return path.
    /// Args: invoker player. Returns true if handled (including rejected/ignored cases with feedback).
    /// </summary>
    public static Func<Player, bool>? TryHandleDebugLeave { get; set; }

    public string KeyWord => "leavebandit";

    public string Usage => "";

    public string Description => "LOCAL DEBUG: leave sg_bandit_hideout via the same return path as Exit (41/109).";

    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public bool Handle(Player invoker, string[] args)
    {
        if (TryHandleDebugLeave is null)
        {
            ChatHelper.SendSystemMessage(invoker, "leavebandit is unavailable (Gateway hook not registered).");
            return true;
        }

        return TryHandleDebugLeave(invoker);
    }
}
