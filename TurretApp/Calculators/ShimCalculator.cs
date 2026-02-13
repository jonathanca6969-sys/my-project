namespace TurretApp.Calculators;

public class ShimResult
{
    public List<(double Size, int Count)> Shims { get; set; } = [];
    public double Total { get; set; }
}

public static class ShimCalculator
{
    public static ShimResult FindCombination(double target, double[] availableShims, double maxOvershim = 0.05, int maxPerSize = 8)
    {
        if (target <= 0.005) return new ShimResult();

        double maxTotal = target + maxOvershim;
        List<(double Size, int Count)>? bestCombo = null;
        double bestTotal = 0;
        double bestScore = double.NegativeInfinity;

        void FindCombos(int index, List<(double Size, int Count)> currentShims, double currentTotal)
        {
            if (currentTotal >= target - 0.005 && currentTotal <= maxTotal + 0.005)
            {
                double diff = currentTotal - target;
                double score = diff >= 0
                    ? (diff <= 0.05 ? 100 - Math.Abs(diff - 0.05) * 20 : 90 - (diff - 0.05) * 50)
                    : diff * 100;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCombo = new(currentShims);
                    bestTotal = currentTotal;
                }
            }

            if (index >= availableShims.Length || currentTotal >= maxTotal) return;

            double shimSize = availableShims[index];
            int maxCount = Math.Min((int)Math.Ceiling((maxTotal - currentTotal) / shimSize), maxPerSize);

            for (int count = 0; count <= maxCount; count++)
            {
                double newTotal = currentTotal + (shimSize * count);
                if (newTotal > maxTotal + 0.01) break;

                var newShims = count > 0
                    ? [..currentShims, (shimSize, count)]
                    : currentShims;

                FindCombos(index + 1, newShims, newTotal);
            }
        }

        FindCombos(0, [], 0);

        if (bestCombo == null)
        {
            // Fallback: greedy
            var result = new List<(double Size, int Count)>();
            double remaining = target;
            foreach (var shimSize in availableShims)
            {
                int count = Math.Min((int)Math.Floor((remaining + 0.005) / shimSize), maxPerSize);
                if (count > 0)
                {
                    result.Add((shimSize, count));
                    remaining -= count * shimSize;
                }
            }
            return new ShimResult
            {
                Shims = result,
                Total = result.Sum(s => s.Size * s.Count)
            };
        }

        return new ShimResult { Shims = bestCombo, Total = bestTotal };
    }
}
