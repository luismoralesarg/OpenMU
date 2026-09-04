// <copyright file="MuApiBridgeConfiguration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

/// <summary>
/// Shared configuration for the mu-api bridge plugins, editable through the
/// AdminPanel's Plugins page.
/// </summary>
public class MuApiBridgeConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the mu-api webhook endpoint, e.g.
    /// "http://mu-api:8081/internal/webhook/event".
    /// </summary>
    public string WebhookUrl { get; set; } = "http://mu-api:8081/internal/webhook/event";

    /// <summary>
    /// Gets or sets the URL of the mu-api character-status webhook endpoint,
    /// e.g. "http://mu-api:8081/internal/webhook/character-status".
    /// </summary>
    public string CharacterStatusWebhookUrl { get; set; } = "http://mu-api:8081/internal/webhook/character-status";

    /// <summary>
    /// Gets or sets the shared secret sent in the "X-Webhook-Secret" header.
    /// Must match the WEBHOOK_SECRET configured on mu-api.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum item level (+N) that triggers a pickup
    /// notification, regardless of item options.
    /// </summary>
    public byte MinimumItemLevelToNotify { get; set; } = 7;

    /// <summary>
    /// Gets or sets a value indicating whether an Excellent or Ancient item
    /// should always notify, even below <see cref="MinimumItemLevelToNotify"/>.
    /// </summary>
    public bool AlwaysNotifyExcellentOrAncient { get; set; } = true;

    /// <summary>
    /// Gets or sets how often, in seconds, the character-status snapshot
    /// (level, map, coordinates, farming state) is sent to mu-api.
    /// </summary>
    public int CharacterStatusReportIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets how many seconds without gaining experience from a kill
    /// must pass before a player is no longer considered to be actively
    /// farming/leveling.
    /// </summary>
    public int FarmingIdleThresholdSeconds { get; set; } = 120;
}
