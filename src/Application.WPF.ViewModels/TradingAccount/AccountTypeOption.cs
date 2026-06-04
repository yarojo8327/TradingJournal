using Application.WPF.Models.Enums;

namespace Application.WPF.ViewModels.TradingAccount;

public sealed record AccountTypeOption(AccountType Value, string Display)
{
    public override string ToString() => Display;
}
