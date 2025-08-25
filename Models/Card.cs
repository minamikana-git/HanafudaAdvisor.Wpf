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
                CardType.Hikari => "��",
                CardType.Tane => "�^�l",
                CardType.Tanzaku => "�Z��",
                CardType.Kasu => "�J�X",
                _ => Type.ToString()
            };
            return $"{Month:D2}:{ty}{(Name is null ? "" : ":" + Name)}";
        }
    }
}
