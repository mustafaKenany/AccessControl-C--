namespace AccessControlPro.Domain.Enums;

public enum MovementType
{
    In = 0,
    Out = 1,
    /// <summary>Goods coming back from a POS refund (stock returns to the shelf).</summary>
    Return = 2,
    /// <summary>Manual stock correction from a stock-take (signed to match the counted quantity).</summary>
    Adjustment = 3
}
