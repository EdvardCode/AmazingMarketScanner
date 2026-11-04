namespace com.edvardcode.AmazingMarketScanner;

public static class MarketAnalyzer
{
    public static string Normalize(string s) =>
        new(s.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static List<AnalysisRow> Analyze(IEnumerable<Offer> offers, IEnumerable<string> targets)
    {
        var all = offers.ToList();
        var rows = new List<AnalysisRow>();

        foreach (var target in targets)
        {
            var tn = Normalize(target);
            var matching = all.Where(o =>
            {
                var on = Normalize(o.ItemName);
                return on.Contains(tn) || tn.Contains(on);
            }).ToList();
            if (matching.Count == 0) continue;

            var ordered = matching.OrderByDescending(o => o.Price).ToList();
            var best = ordered[0];
            var avg = (long)matching.Average(o => o.Price);

            rows.Add(new AnalysisRow(target, best.Price, best.Pavilion, avg, best.Price - avg,
                matching.Count,
                string.Join("  |  ", ordered.Select(o => $"{o.Price:N0} (№{o.Pavilion})"))));
        }
        return rows.OrderByDescending(r => r.GainVsAvg).ToList();
    }
}