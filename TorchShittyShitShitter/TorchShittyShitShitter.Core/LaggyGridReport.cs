namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Carry around a laggy grid's metadata.
    /// </summary>
    public class LaggyGridReport
    {
        public LaggyGridReport(long gridId,
            double mspf,
            string gridName,
            string factionTag = null,
            string playerName = null)
        {
            GridId = gridId;
            Mspf = mspf;
            GridName = gridName;
            FactionTagOrNull = factionTag;
            PlayerNameOrNull = playerName;
        }

        public long GridId { get; }
        public double Mspf { get; }
        public string GridName { get; }
        public string FactionTagOrNull { get; }
        public string PlayerNameOrNull { get; }

        public override string ToString()
        {
            var name = FactionTagOrNull ?? PlayerNameOrNull ?? GridName;
            return $"(\"{name}\" (\"{GridName}\"), {Mspf:0.00}ms/f)";
        }
    }
}