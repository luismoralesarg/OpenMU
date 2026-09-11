// <copyright file="PlayerSessionEndedNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies mu-api whenever a player's character disconnects, so the
/// mobile app can log/push "tu personaje se desconectó" - the symmetric
/// counterpart to <see cref="PlayerSessionStartedNotificationPlugIn"/>.
/// </summary>
/// <remarks>
/// Fires on the transition into <see cref="PlayerState.Disconnected"/> -
/// every in-game state (entered world, trading, dead, changing map, an
/// open NPC dialog, ...) can reach it directly, so instead of enumerating
/// every possible previous state this just checks
/// <see cref="Player.SelectedCharacter"/>: a player who disconnects from
/// the character selection screen (or earlier - login, authentication)
/// never selected one, so there's nothing meaningful to report. This
/// plugin point (<see cref="Player.PlayerState"/>'s <c>StateChanged</c>
/// event, see Player.cs) fires before the disconnect's own cleanup
/// (<c>InternalDisconnectAsync</c>), so <see cref="Player.Account"/> and
/// <see cref="Player.SelectedCharacter"/> are still populated here.
/// </remarks>
[PlugIn]
[Display(Name = "mu-api: Session Ended Notification", Description = "Sends a webhook to mu-api when a player's character disconnects.")]
[Guid("6F3A8B2E-1D4C-4E9F-A1B7-3C6D8E2F5A90")]
public class PlayerSessionEndedNotificationPlugIn : IPlayerStateChangedPlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        if (currentState != PlayerState.Disconnected)
        {
            return;
        }

        if (player.Account is not { } account || player.SelectedCharacter is not { } character)
        {
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();

        var payload = new WebhookEventPayload(
            Type: "session_ended",
            AccountName: account.LoginName,
            CharacterName: character.Name,
            Detail: "conexión finalizada");

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
