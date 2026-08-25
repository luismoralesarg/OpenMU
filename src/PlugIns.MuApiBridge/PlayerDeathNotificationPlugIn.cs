// <copyright file="PlayerDeathNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies mu-api whenever a player dies, so the mobile app can push
/// "your character died" to the account's registered devices.
/// </summary>
[PlugIn]
[Display(Name = "mu-api: Player Death Notification", Description = "Sends a webhook to mu-api when a player dies.")]
[Guid("9F6E9C9A-6E38-4C7E-9C36-6F6D9C9E0E31")]
public class PlayerDeathNotificationPlugIn : IAttackableGotKilledPlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public async ValueTask AttackableGotKilledAsync(IAttackable killed, IAttacker? killer)
    {
        if (killed is not Player player || player.Account is not { } account)
        {
            // Monsters, guards, etc. - not our concern here.
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();

        var killerName = player.LastDeath?.KillerName;
        var detail = string.IsNullOrEmpty(killerName) ? "muerte" : killerName;

        var payload = new WebhookEventPayload(
            Type: "player_died",
            AccountName: account.LoginName,
            CharacterName: player.SelectedCharacter?.Name ?? string.Empty,
            Detail: detail);

        await MuApiWebhookClient.SendAsync(this.Configuration, payload, player.Logger).ConfigureAwait(false);
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
