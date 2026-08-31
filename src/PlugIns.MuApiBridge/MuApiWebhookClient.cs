// <copyright file="MuApiWebhookClient.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Posts gameplay events to the mu-api webhook
/// (POST /internal/webhook/event). Shared by every notification plugin in
/// this assembly so there is exactly one <see cref="HttpClient"/> for the
/// process lifetime, instead of one per plugin instance.
/// </summary>
public static class MuApiWebhookClient
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>
    /// Sends one event to mu-api. Never throws - a notification failure
    /// must never break gameplay, so errors are only logged.
    /// </summary>
    /// <param name="configuration">The plugin configuration holding the webhook URL and secret.</param>
    /// <param name="payload">The event payload.</param>
    /// <param name="logger">The logger to report failures to.</param>
    public static async ValueTask SendAsync(MuApiBridgeConfiguration configuration, WebhookEventPayload payload, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(configuration.WebhookUrl))
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, configuration.WebhookUrl)
            {
                Content = JsonContent.Create(payload),
            };
            request.Headers.Add("X-Webhook-Secret", configuration.WebhookSecret);

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "mu-api webhook returned {StatusCode} for event {EventType} of account {AccountName}",
                    response.StatusCode,
                    payload.Type,
                    payload.AccountName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send mu-api webhook event {EventType} for account {AccountName}", payload.Type, payload.AccountName);
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
