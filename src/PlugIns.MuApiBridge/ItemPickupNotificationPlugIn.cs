// <copyright file="ItemPickupNotificationPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Notifies mu-api when a player picks up a notable item (high refine level,
/// Excellent or Ancient), so the mobile app can push "you got a good drop".
/// </summary>
/// <remarks>
/// There's no plugin point fired on item pickup (<see cref="Player.PlayerPickedUpItem"/>
/// is a plain event, not a <c>[PlugInPoint]</c>), so this plugin rides on the
/// periodic-task mechanism to subscribe to that event on every connected
/// player exactly once. <see cref="ConditionalWeakTable{TKey,TValue}"/> keeps
/// track of who's already subscribed without keeping players alive longer
/// than they otherwise would be.
/// </remarks>
[PlugIn]
[Display(Name = "mu-api: Item Pickup Notification", Description = "Sends a webhook to mu-api when a player picks up a notable item.")]
[Guid("3D4B7B7A-6C90-4E36-8C0C-8B7B6B0B2B4E")]
public class ItemPickupNotificationPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConditionalWeakTable<Player, object> SubscribedPlayers = new();

    private DateTime _nextRunUtc = DateTime.UtcNow;

    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (DateTime.UtcNow < this._nextRunUtc)
        {
            return;
        }

        this._nextRunUtc = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        this.Configuration ??= this.CreateDefaultConfiguration();

        var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players)
        {
            if (SubscribedPlayers.TryGetValue(player, out _))
            {
                continue;
            }

            SubscribedPlayers.Add(player, this);
            player.PlayerPickedUpItem += this.OnPlayerPickedUpItemAsync;
            player.PlayerDisconnected += _ =>
            {
                player.PlayerPickedUpItem -= this.OnPlayerPickedUpItemAsync;
                return ValueTask.CompletedTask;
            };
        }
    }

    /// <inheritdoc/>
    public void ForceStart()
    {
        this._nextRunUtc = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public object CreateDefaultConfig()
    {
        return this.CreateDefaultConfiguration();
    }

    private async ValueTask OnPlayerPickedUpItemAsync((Player Player, ILocateable Item) args)
    {
        if (args.Item is not DroppedItem { Item: { } item })
        {
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();
        if (!this.IsNotable(item))
        {
            return;
        }

        var player = args.Player;
        if (player.Account is not { } account)
        {
            return;
        }

        var itemName = item.Definition?.GetNameForLevel(item.Level) ?? "Ítem";
        var detail = item.Level > 0 ? $"{itemName} +{item.Level}" : itemName;

        var payload = new WebhookEventPayload(
            Type: "item_picked_up",
            AccountName: account.LoginName,
            CharacterName: player.SelectedCharacter?.Name ?? string.Empty,
            Detail: detail);

        await MuApiWebhookClient.SendAsync(this.Configuration, payload, player.Logger).ConfigureAwait(false);
    }

    private bool IsNotable(Item item)
    {
        var configuration = this.Configuration!;

        if (item.Level >= configuration.MinimumItemLevelToNotify)
        {
            return true;
        }

        if (!configuration.AlwaysNotifyExcellentOrAncient)
        {
            return false;
        }

        return item.ItemOptions.Any(o =>
            o.ItemOption?.OptionType == ItemOptionTypes.Excellent
            || o.ItemOption?.OptionType == ItemOptionTypes.AncientOption);
    }

    private MuApiBridgeConfiguration CreateDefaultConfiguration()
    {
        return new MuApiBridgeConfiguration();
    }
}
