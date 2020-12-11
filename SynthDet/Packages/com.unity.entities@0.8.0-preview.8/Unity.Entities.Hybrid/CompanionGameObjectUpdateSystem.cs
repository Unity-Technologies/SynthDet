#if !UNITY_DISABLE_MANAGED_COMPONENTS

/*
 * Hybrid Components are classic Unity components that are added to an entity via AddComponentObject.
 * We call Companion GameObject the GameObject owned by an entity in order to host those Hybrid Components.
 * Companion GameObjects should be considered implementation details and are not intended to be directly accessed by users.
 * An entity can also have Hybrid Components owned externally (this is used during conversion), but this is not what the system below is about.
 * When an entity owns a Companion GameObject, the entity also has a managed CompanionLink, which contains a reference to that GameObject.
 * Companion GameObjects are in world space, their transform is updated from their entities, never the other way around.
 * Getting to the Companion GameObject from an Entity is done through the managed CompanionLink.
 * Going the other way around, from the Companion GameObject to the Entity, isn't possible nor advised.
 */

using Unity.Entities;
using UnityEngine;

// Needs to be a system state because instantiation will always create disabled GameObjects
struct CompanionGameObjectActiveSystemState : ISystemStateComponentData { }

[ExecuteAlways]
class CompanionGameObjectUpdateSystem : ComponentSystem
{
    protected override void OnUpdate()
    {
        var toActivate = Entities.WithNone<CompanionGameObjectActiveSystemState>().WithAll<CompanionLink>();
        toActivate.ForEach((CompanionLink link) => link.Companion.SetActive(true));
        EntityManager.AddComponent<CompanionGameObjectActiveSystemState>(toActivate.ToEntityQuery());

        var toDeactivate = Entities.WithAny<Disabled, Prefab>().WithAll<CompanionGameObjectActiveSystemState, CompanionLink>();
        toDeactivate.ForEach((CompanionLink link) => link.Companion.SetActive(false));
        EntityManager.RemoveComponent<CompanionGameObjectActiveSystemState>(toDeactivate.ToEntityQuery());

        var activeSystemStateCleanup = Entities.WithNone<CompanionLink>().WithAll<CompanionGameObjectActiveSystemState>().ToEntityQuery();
        EntityManager.RemoveComponent<CompanionGameObjectActiveSystemState>(activeSystemStateCleanup);
    }
}

#endif
