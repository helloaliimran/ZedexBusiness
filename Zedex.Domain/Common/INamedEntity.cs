namespace Zedex.Domain.Common;

/// <summary>Simple lookup entities with a unique Name (Category, Color, Gauge).</summary>
public interface INamedEntity
{
    string Name { get; set; }
}
