using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

public class Category : BaseEntity, INamedEntity
{
    public string Name { get; set; } = default!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Color : BaseEntity, INamedEntity
{
    public string Name { get; set; } = default!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Gauge : BaseEntity, INamedEntity
{
    public string Name { get; set; } = default!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>Manufacturer/brand of a PVC product (e.g. section company).</summary>
public class Company : BaseEntity, INamedEntity
{
    public string Name { get; set; } = default!;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
