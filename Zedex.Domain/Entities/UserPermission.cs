using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

/// <summary>Per-worker module toggles (1:1 with Identity user). Admins bypass.</summary>
public class UserPermission : BaseEntity
{
    public string UserId { get; set; } = default!;

    public bool Dashboard { get; set; }
    public bool Products { get; set; }
    public bool Stock { get; set; }
    public bool Billing { get; set; }
    public bool Customers { get; set; }
    public bool CustomerLedger { get; set; }
    public bool Reports { get; set; }

    public bool Has(AppModule module) => module switch
    {
        AppModule.Dashboard => Dashboard,
        AppModule.Products => Products,
        AppModule.Stock => Stock,
        AppModule.Billing => Billing,
        AppModule.Customers => Customers,
        AppModule.CustomerLedger => CustomerLedger,
        AppModule.Reports => Reports,
        _ => false
    };
}
