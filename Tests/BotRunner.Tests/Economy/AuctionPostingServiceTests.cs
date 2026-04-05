using BotRunner.Tasks.Economy;

namespace BotRunner.Tests.Economy;

public class AuctionPostingServiceTests
{
    [Fact]
    public void RecordPrice_StoresPrice()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(1234, 50000);

        Assert.Equal(50000u, svc.GetMarketPrice(1234));
    }

    [Fact]
    public void GetMarketPrice_ReturnsCached()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(100, 20000);

        var price = svc.GetMarketPrice(100);

        Assert.NotNull(price);
        Assert.Equal(20000u, price!.Value);
    }

    [Fact]
    public void GetMarketPrice_ReturnsNull_WhenNotCached()
    {
        var svc = new AuctionPostingService();

        Assert.Null(svc.GetMarketPrice(9999));
    }

    [Fact]
    public void EvaluatePosting_Undercuts5Percent()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(100, 10000); // 1g market price

        var decision = svc.EvaluatePosting(100, 1UL, vendorSellPrice: 100);

        Assert.NotNull(decision);
        // 10000 * 0.95 = 9500
        Assert.Equal(9500u, decision!.BuyoutCopper);
        Assert.Equal(9500u / 2, decision.StartBidCopper);
    }

    [Fact]
    public void EvaluatePosting_RejectsWhenBelowVendorPrice()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(100, 1000); // 10s market price

        // Undercut = 950, vendor = 1000 => should reject
        var decision = svc.EvaluatePosting(100, 1UL, vendorSellPrice: 1000);

        Assert.Null(decision);
    }

    [Fact]
    public void EvaluatePosting_Uses3xVendorWhenNoMarketData()
    {
        var svc = new AuctionPostingService();

        var decision = svc.EvaluatePosting(100, 1UL, vendorSellPrice: 500);

        Assert.NotNull(decision);
        Assert.Equal(1500u, decision!.BuyoutCopper); // 500 * 3
        Assert.Equal(750u, decision.StartBidCopper);  // 1500 / 2
    }

    [Fact]
    public void EvaluatePosting_ReturnsNull_WhenNoMarketDataAndZeroVendor()
    {
        var svc = new AuctionPostingService();

        var decision = svc.EvaluatePosting(100, 1UL, vendorSellPrice: 0);

        Assert.Null(decision);
    }

    [Fact]
    public void PurgeStale_RemovesOldPrices()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(1, 100);
        svc.RecordPrice(2, 200);

        // All prices are fresh (just recorded), so purging with 0 maxAge removes all
        svc.PurgeStale(TimeSpan.Zero);

        Assert.Empty(svc.GetAllPrices());
    }

    [Fact]
    public void PurgeStale_KeepsFreshPrices()
    {
        var svc = new AuctionPostingService();
        svc.RecordPrice(1, 100);
        svc.RecordPrice(2, 200);

        // Purging with 1 hour maxAge keeps all fresh prices
        svc.PurgeStale(TimeSpan.FromHours(1));

        Assert.Equal(2, svc.GetAllPrices().Count);
    }
}
