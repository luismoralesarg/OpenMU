// <copyright file="PlayerLevelUpNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies mu-api whenever a player levels up, so the mobile app can show
/// it on the Logs screen (and push "subiste de nivel" to the account's
/// registered devices).
/// </summary>
[PlugIn]
[Display(Name = "mu-api: Level Up Notification", Description = "Sends a webhook to mu-api when a player levels up.")]
[Guid("6A6C9E2A-6B7F-4E7C-9C29-2C6F6B7B5C41")]
public class PlayerLevelUpNotificationPlugIn : ICharacterLevelUpPlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public void CharacterLeveledUp(Player player)
    {
        if (player.Account is not { } account)
        {
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();

        var payload = new WebhookEventPayload(
            Type: "level_up",
            AccountName: account.LoginName,
            CharacterName: player.SelectedCharacter?.Name ?? string.Empty,
            Detail: $"nivel {player.Level}");

        // CharacterLeveledUp isn't async, so this fires without awaiting -
        // same as every other "never block gameplay" webhook here, just
        // without a ValueTask to hand back.
        _ = MuApiWebhookClient.SendAsync(this.Configuration, payload, player.Logger).AsTask();
    }

    /// <inheritdoc/>
    public object CreateDefaultConfig()
    {
        return this.CreateDefaultConfiguration();
    }

    private MuApiBridgeConfiguration CreateDefaultConfiguration()
    {
        return new MuApiBridgeConfiguration();
    }
}
