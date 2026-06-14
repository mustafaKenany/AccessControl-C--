namespace AccessControlPro.Domain.Enums;

public enum PaymentMethod
{
    Cash = 0,
    CardBalance = 1,
    Credit = 2      // POS sale "on account" — added to the player's Debt, collected later
}
