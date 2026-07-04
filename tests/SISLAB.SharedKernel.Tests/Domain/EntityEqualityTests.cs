using SISLAB.SharedKernel.Domain;

namespace SISLAB.SharedKernel.Tests.Domain;

/// <summary>
/// Testes de igualdade por identidade da classe base <see cref="Entity{TId}"/>.
/// </summary>
public sealed class EntityEqualityTests
{
    // Implementação concreta mínima para os testes
    private sealed class SampleEntity : Entity<Guid>
    {
        public SampleEntity(Guid id) : base(id) { }
    }

    private sealed class AnotherEntity : Entity<Guid>
    {
        public AnotherEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void TwoEntities_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var first = new SampleEntity(id);
        var second = new SampleEntity(id);

        Assert.Equal(first, second);
    }

    [Fact]
    public void TwoEntities_WithDifferentIds_ShouldNotBeEqual()
    {
        var first = new SampleEntity(Guid.NewGuid());
        var second = new SampleEntity(Guid.NewGuid());

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TwoEntities_WithSameId_ButDifferentTypes_ShouldNotBeEqual()
    {
        var id = Guid.NewGuid();
        var sample = new SampleEntity(id);
        var another = new AnotherEntity(id);

        // Tipos diferentes => não iguais, mesmo com mesmo ID
        Assert.NotEqual<Entity<Guid>>(sample, another);
    }

    [Fact]
    public void Entity_ShouldUseReferenceEquality_WhenSameInstance()
    {
        var entity = new SampleEntity(Guid.NewGuid());

        Assert.Equal(entity, entity);
    }

    [Fact]
    public void EqualityOperator_ShouldReturnTrue_ForEntitiesWithSameId()
    {
        var id = Guid.NewGuid();
        var first = new SampleEntity(id);
        var second = new SampleEntity(id);

        Assert.True(first == second);
        Assert.False(first != second);
    }

    [Fact]
    public void GetHashCode_ShouldBeEqual_ForEntitiesWithSameId()
    {
        var id = Guid.NewGuid();
        var first = new SampleEntity(id);
        var second = new SampleEntity(id);

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entity_WithNullComparison_ShouldReturnFalse()
    {
        var entity = new SampleEntity(Guid.NewGuid());

        Assert.False(entity.Equals(null));
    }
}
