namespace HanafudaAdvisor.Wpf.Models
{
    public enum CardType { Hikari, Tane, Tanzaku, Kasu }

    public sealed record Card(
        int Month,
        CardType Type,
        string? Name = null,
        bool IsRain = false,
        bool IsRedPoem = false,
        bool IsBlueRibbon = false,
        bool IsBoar = false,
        bool IsDeer = false,
        bool IsButterfly = false,
        bool IsCup = false)
    {
        public override string ToString()
        {
            string ty = Type switch
            {
                CardType.Hikari => "光",
                CardType.Tane => "タネ",
                CardType.Tanzaku => "短冊",
                CardType.Kasu => "カス",
                _ => Type.ToString()
            };
            return $"{Month:D2}:{ty}{(Name is null ? "" : ":" + Name)}";
        }
    }
}
