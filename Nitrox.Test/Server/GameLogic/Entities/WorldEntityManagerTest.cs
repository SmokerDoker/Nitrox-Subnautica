using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using NSubstitute;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class WorldEntityManagerTest
{
    [TestMethod]
    public void PruneDestroyedBaseLeakChildren_RemovesLeaksNotInRegistry()
    {
        EntityRegistry entityRegistry = new(Substitute.For<ILogger<EntityRegistry>>());
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.Id = new();

        BaseLeakEntity existingLeak = new(75f, new(1, 2, 3), new(), buildEntity.Id);
        BaseLeakEntity destroyedLeak = new(25f, new(4, 5, 6), new(), buildEntity.Id);
        buildEntity.ChildEntities = [existingLeak, destroyedLeak];

        entityRegistry.AddEntity(existingLeak);

        WorldEntityManager.PruneDestroyedBaseLeakChildren(buildEntity, entityRegistry);

        buildEntity.ChildEntities.Should().ContainSingle(child => child.Id == existingLeak.Id);
        buildEntity.ChildEntities.Should().NotContain(child => child.Id == destroyedLeak.Id);
    }
}
