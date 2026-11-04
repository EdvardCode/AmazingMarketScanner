namespace com.edvardcode.AmazingMarketScanner;

public record Offer(string ItemName, long Price, int Pavilion);

public record AnalysisRow(string Item, long BestPrice, int BestPavilion,
                          long AvgPrice, long GainVsAvg, int OfferCount, string AllPrices)
{
    public string PavilionDisplay => $"№{BestPavilion}";
}

public enum PavilionStatus { Pending, Scanned, Closed }