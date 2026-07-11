namespace MediaVault.LinkHub.Domain.Common;

/// <summary>
/// Clase base para entidades persistidas con identificador entero autogenerado.
/// </summary>
public abstract class EntityBase
{
    public int Id { get; set; }
}
