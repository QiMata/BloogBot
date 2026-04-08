using GameData.Core.Interfaces;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Economy;

/// <summary>
/// Manages AH posting strategy: scan prices, post with undercut, re-list unsold items.
/// Uses CMSG_AUCTION_LIST_ITEMS for scanning and CMSG_AUCTION_SELL_ITEM for posting.
/// </summary>
public class AuctionPostingService
{
    /// <summary>Price memory for market tracking.</summary>
    public record MarketPrice(uint ItemId, uint BuyoutCopper, DateTime LastSeen);

    /// <summary>Posting decision for an item.</summary>
    public record PostingDecision(uint ItemId, ulong ItemGuid, uint BuyoutCopper, uint StartBidCopper, int AuctionDurationHours);

    private readonly ConcurrentDictionary<uint, MarketPrice> _priceCache = new();
    private const float UndercutPercent = 0.05f; // 5% undercut
    private const int DefaultAuctionHours = 24;

    /// <summary>Record a market price observation from AH scan.</summary>
    public void RecordPrice(uint itemId, uint buyoutCopper)
    {
        _priceCache[itemId] = new MarketPrice(itemId, buyoutCopper, DateTime.UtcNow);
    }

    /// <summary>Get the current market price for an item.</summary>
    public uint? GetMarketPrice(uint itemId)
    {
        if (_priceCache.TryGetValue(itemId, out var price))
        {
            // Only trust prices less than 24h old
            if ((DateTime.UtcNow - price.LastSeen).TotalHours < 24)
                return price.BuyoutCopper;
        }
        return null;
    }

    /// <summary>
    /// Decide posting parameters for an item based on market data.
    /// Returns null if item shouldn't be posted (no market data or too cheap).
    /// </summary>
    public PostingDecision? EvaluatePosting(uint itemId, ulong itemGuid, uint vendorSellPrice)
    {
        var marketPrice = GetMarketPrice(itemId);
        if (marketPrice == null)
        {
            // No market data — post at 3x vendor price as baseline
            if (vendorSellPrice == 0) return null;
            var baselinePrice = vendorSellPrice * 3;
            return new PostingDecision(itemId, itemGuid, baselinePrice, baselinePrice / 2, DefaultAuctionHours);
        }

        // Undercut by 5%
        var buyout = (uint)(marketPrice.Value * (1f - UndercutPercent));
        var startBid = buyout / 2;

        // Don't post if undercut price is less than vendor sell price
        if (buyout <= vendorSellPrice)
        {
            Log.Debug("[AH] Item {ItemId} not worth posting — undercut ({Buyout}c) <= vendor ({Vendor}c)",
                itemId, buyout, vendorSellPrice);
            return null;
        }

        return new PostingDecision(itemId, itemGuid, buyout, startBid, DefaultAuctionHours);
    }

    /// <summary>Get all tracked market prices.</summary>
    public IReadOnlyList<MarketPrice> GetAllPrices()
        => _priceCache.Values.ToList();

    /// <summary>Clear stale prices older than the given threshold.</summary>
    public void PurgeStale(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var staleKeys = _priceCache
            .Where(kv => kv.Value.LastSeen < cutoff)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in staleKeys)
            _priceCache.TryRemove(key, out _);
    }
}
