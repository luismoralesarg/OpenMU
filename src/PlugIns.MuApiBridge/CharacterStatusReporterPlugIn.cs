// <copyright file="CharacterStatusReporterPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.PlugIns.MuApiBridge;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Periodically sends a status snapshot (level, map, coordinates, farming
/// state) of every connected player to mu-api, so the mobile app can show
/// "where is my character and is it leveling".
/// </summary>
/// <remarks>
/// "Farming" is derived from <see cref="IPlayerGainedExperiencePlugIn"/>:
/// every kill that grants experience refreshes a per-player timestamp kept
/// in a <see cref="ConditionalWeakTable{TKey,TValue}"/> (so it never keeps a
/// disconnected player alive). If more than
/// <see cref="MuApiBridgeConfiguration.FarmingIdleThresholdSeconds"/> have
/// passed since the last kill, the player is reported as not farming.
/// </remarks>
[PlugIn]
[Display(Name = "mu-api: Character Status Reporter", Description = "Periodically sends level/map/coordinates/farming status to mu-api.")]
[Guid("6E1A7B9C-2D3F-4A5E-9B6C-1F8D3A7E4C21")]
public class CharacterStatusReporterPlugIn : IPeriodicTaskPlugIn, IPlayerGainedExperiencePlugIn, ISupportCustomConfiguration<MuApiBridgeConfiguration>, ISupportDefaultCustomConfiguration
{
    private static readonly ConditionalWeakTable<Player, StrongBox<DateTime>> LastKillUtc = new();

    private DateTime _nextRunUtc = DateTime.UtcNow;

    /// <inheritdoc/>
    public MuApiBridgeConfiguration? Configuration { get; set; }

    /// <inheritdoc/>
    public ValueTask PlayerGainedExperienceAsync(Player player, int experience, IAttackable killedObject, bool isMasterExperience)
    {
        if (experience <= 0)
        {
            return ValueTask.CompletedTask;
        }

        var lastKill = LastKillUtc.GetOrCreateValue(player);
        lastKill.Value = DateTime.UtcNow;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        if (DateTime.UtcNow < this._nextRunUtc)
        {
            return;
        }

        this.Configuration ??= this.CreateDefaultConfiguration();
        this._nextRunUtc = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, this.Configuration.CharacterStatusReportIntervalSeconds));

        var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
        foreach (var player in players)
        {
            await this.ReportPlayerStatusAsync(player).ConfigureAwait(false);
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

    private async ValueTask ReportPlayerStatusAsync(Player player)
    {
        if (player.Account is not { } account || player.SelectedCharacter is not { } character)
        {
            return;
        }

        var configuration = this.Configuration!;
        var isFarming = LastKillUtc.TryGetValue(player, out var lastKill)
            && (DateTime.UtcNow - lastKill.Value).TotalSeconds <= configuration.FarmingIdleThresholdSeconds;

        var mapDefinition = player.CurrentMap?.Definition;
        var payload = new CharacterStatusPayload(
            AccountName: account.LoginName,
            CharacterName: character.Name,
            Level: player.Level,
            MapName: mapDefinition?.Name.ToString() ?? string.Empty,
            MapNumber: mapDefinition?.Number ?? 0,
            PositionX: player.Position.X,
            PositionY: player.Position.Y,
            IsFarming: isFarming);

        await MuApiWebhookClient.SendStatusAsync(configuration, payload, player.Logger).ConfigureAwait(false);
    }

    private MuApiBridgeConfiguration CreateDefaultConfiguration()
    {
        return new MuApiBridgeConfiguration();
    }
}
