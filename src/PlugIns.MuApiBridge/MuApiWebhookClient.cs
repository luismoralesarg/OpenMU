// <copyright file="MuApiWebhookClient.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Posts gameplay events and status snapshots to mu-api. Shared by every
/// notification plugin in this assembly so there is exactly one
/// <see cref="HttpClient"/> for the process lifetime, instead of one per
/// plugin instance.
/// </summary>
public static class MuApiWebhookClient
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Sends one event to mu-api's event webhook (POST /internal/webhook/event).
    /// Never throws - a notification failure must never break gameplay, so
    /// errors are only logged.
    /// </summary>
    /// <param name="configuration">The plugin configuration holding the webhook URL and secret.</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="logger">The logger to report failures to.</param>
    public static async ValueTask SendAsync(MuApiBridgeConfiguration configuration, WebhookEventPayload payload, ILogger logger)
    {
        await PostAsync(configuration.WebhookUrl, configuration.WebhookSecret, payload, logger, payload.Type, payload.AccountName).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a character-status snapshot to mu-api's status webhook
    /// (POST /internal/webhook/character-status). Never throws - a
    /// notification failure must never break gameplay, so errors are only
    /// logged.
    /// </summary>
    /// <param name="configuration">The plugin configuration holding the webhook URL and secret.</param>
    /// <param name="payload">The status snapshot payload.</param>
    /// <param name="logger">The logger to report failures to.</param>
    public static async ValueTask SendStatusAsync(MuApiBridgeConfiguration configuration, CharacterStatusPayload payload, ILogger logger)
    {
        await PostAsync(configuration.CharacterStatusWebhookUrl, configuration.WebhookSecret, payload, logger, "character_status", payload.AccountName).ConfigureAwait(false);
    }

    private static async ValueTask PostAsync<TPayload>(string url, string secret, TPayload payload, ILogger logger, string eventType, string accountName)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Add("X-Webhook-Secret", secret);

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "mu-api webhook returned {StatusCode} for event {EventType} of account {AccountName}",
                    response.StatusCode,
                    eventType,
                    accountName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send mu-api webhook event {EventType} for account {AccountName}", eventType, accountName);
        }
    }
}

/// <summary>
/// Wire format of one event, matching mu-api's
/// internal/adapters/inbound/http/webhook_handler.go#gameEventRequest.
/// </summary>
/// <param name="Type">Either "player_died" or "item_picked_up".</param>
/// <param name="AccountName">The account's login name.</param>
/// <param name="CharacterName">The character's name.</param>
/// <param name="Detail">Killer name for deaths, item designation for pickups.</param>
public record WebhookEventPayload(string Type, string AccountName, string CharacterName, string Detail);

/// <summary>
/// Wire format of one character-status snapshot, matching mu-api's
/// internal/adapters/inbound/http/characterstatus_handler.go#reportRequest.
/// </summary>
/// <param name="AccountName">The account's login name.</param>
/// <param name="CharacterName">The character's name.</param>
/// <param name="Level">The character's current level.</param>
/// <param name="MapName">The name of the map the character is currently on.</param>
/// <param name="MapNumber">The numeric id of the map the character is currently on.</param>
/// <param name="PositionX">The character's X coordinate on the map.</param>
/// <param name="PositionY">The character's Y coordinate on the map.</param>
/// <param name="IsFarming">Whether the character has gained experience from a kill recently enough to be considered actively farming/leveling.</param>
public record CharacterStatusPayload(
    string AccountName,
    string CharacterName,
    int Level,
    string MapName,
    short MapNumber,
    byte PositionX,
    byte PositionY,
    bool IsFarming);
