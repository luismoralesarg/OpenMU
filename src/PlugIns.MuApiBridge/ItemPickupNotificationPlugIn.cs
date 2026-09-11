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
    /// <summary>
    /// The classic MU jewels, identified by (Group, Number) - there's no
    /// "is this a jewel" flag on <see cref="ItemDefinition"/>. Mirrors
    /// mu-api's domain.JewelKinds exactly (internal/domain/jewel.go) -
    /// keep both lists in sync if a jewel is ever added or renamed.
    /// </summary>
    private static readonly IReadOnlyDictionary<(byte Group, short Number), string> JewelNamesByGroupAndNumber =
        new Dictionary<(byte, short), string>
        {
            [(14, 13)] = "Jewel of Bless",
            [(14, 14)] = "Jewel of Soul",
            [(12, 15)] = "Jewel of Chaos",
            [(14, 16)] = "Jewel of Life",
            [(14, 22)] = "Jewel of Creation",
            [(14, 31)] = "Jewel of Guardian",
            [(14, 41)] = "Gemstone",
            [(14, 42)] = "Jewel of Harmony",

            // Bundles - a separate ItemDefinition per
            // Persistence/Initialization/*/Items/PackedJewels.cs, not
            // just a different Level of the row above. See
            // PackedJewelGroupAndNumbers/TryGetJewelDetail for how their
            // quantity is computed differently (Durability is always 1
            // on these).
            [(12, 30)] = "Jewel of Bless",
            [(12, 31)] = "Jewel of Soul",
            [(12, 141)] = "Jewel of Chaos",
            [(12, 136)] = "Jewel of Life",
            [(12, 137)] = "Jewel of Creation",
            [(12, 138)] = "Jewel of Guardian",
            [(12, 139)] = "Gemstone",
            [(12, 140)] = "Jewel of Harmony",
        };

    /// <summary>
    /// The subset of <see cref="JewelNamesByGroupAndNumber"/> that are
    /// packed/"bundle" variants - their quantity comes from
    /// <c>(Level+1)*10</c> instead of <see cref="Item.Durability"/> (which
    /// is always 1 on these). The multiplier is only documented in
    /// GameLogic/ItemPriceCalculator.cs's pricing formula.
    /// </summary>
    private static readonly IReadOnlySet<(byte Group, short Number)> PackedJewelGroupAndNumbers = new HashSet<(byte, short)>
    {
        (12, 30), (12, 31), (12, 136), (12, 137), (12, 138), (12, 139), (12, 140), (12, 141),
    };

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

        var player = args.Player;
        if (player.Account is not { } account)
        {
            return;
        }

        var accountName = account.LoginName;
        var characterName = player.SelectedCharacter?.Name ?? string.Empty;

        // Jewels never meet IsNotable's level/Excellent/Ancient bar (they
        // don't have a refine level or item options), so this is reported
        // as its own event type regardless of that check.
        if (this.TryGetJewelDetail(item, out var jewelDetail))
        {
            var jewelPayload = new WebhookEventPayload(
                Type: "jewel_picked_up",
                AccountName: accountName,
                CharacterName: characterName,
                Detail: jewelDetail);

            await MuApiWebhookClient.SendAsync(this.Configuration, jewelPayload, player.Logger).ConfigureAwait(false);
            return;
        }

        if (!this.IsNotable(item))
        {
            return;
        }

        var itemName = item.Definition?.GetNameForLevel(item.Level) ?? "Ítem";
        var detail = item.Level > 0 ? $"{itemName} +{item.Level}" : itemName;

        var payload = new WebhookEventPayload(
            Type: "item_picked_up",
            AccountName: accountName,
            CharacterName: characterName,
            Detail: detail);

        await MuApiWebhookClient.SendAsync(this.Configuration, payload, player.Logger).ConfigureAwait(false);
    }

    private bool TryGetJewelDetail(Item item, out string detail)
    {
        detail = string.Empty;
        if (item.Definition is not { } definition)
        {
            return false;
        }

        if (!JewelNamesByGroupAndNumber.TryGetValue((definition.Group, definition.Number), out var jewelName))
        {
            return false;
        }

        var quantity = PackedJewelGroupAndNumbers.Contains((definition.Group, definition.Number))
            ? (item.Level + 1) * 10
            : (int)Math.Round(item.Durability);
        detail = quantity > 1 ? $"{jewelName} x{quantity}" : jewelName;
        return true;
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
