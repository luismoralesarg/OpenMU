// <copyright file="PlayerSessionStartedNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies mu-api whenever a player's character enters the game world
/// (finished character selection and started playing), so the mobile app
/// can log/push "tu cuenta se conectó".
/// </summary>
/// <remarks>
/// Mirrors the built-in <see cref="ShowMessageToAllWhenPlayerEnteredWorldPlugIn"/>'s
/// own state-transition check for "just entered the game".
/// </remarks>
[PlugIn]
[Display(Name = "mu-api: Session Started Notification", Description = "Sends a webhook to mu-api when a player's character enters the game.")]
[Guid("2B8E7D3C-4F1A-4A9E-8B2D-7C5A9E3D6F18")]
public class PlayerSessionStartedNotificationPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld)
        {
            return;
        }

        if (player.Account is not { } account)
        {
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();

        var payload = new WebhookEventPayload(
            Type: "session_started",
            AccountName: account.LoginName,
            CharacterName: player.SelectedCharacter?.Name ?? string.Empty,
            Detail: "conexión iniciada");

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
