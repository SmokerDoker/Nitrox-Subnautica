using System;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic.ModCompat.ReturnOfTheAncients;

/// <summary>
/// Soft-dependency compatibility patch for the "Return of the Ancients" mod (Gargantuan Leviathan). The mod is
/// closed-source and not referenced at compile time, so the target type/method is resolved by name; the patch is
/// simply skipped if the mod isn't installed.
/// </summary>
/// <remarks>
/// RotA's <c>VoidGargSpawner</c> independently detects "player entered the void" on each client and locally
/// instantiates the Gargantuan via <c>Object.Instantiate</c>, with no multiplayer awareness at all. Left alone,
/// every client that enters the void would spawn its own un-networked Gargantuan instance.
/// <para/>
/// <c>VoidGargSingleton.Start()</c> runs exactly once for whichever Gargantuan instance is currently alive
/// (it's added to the prefab itself, and sets a static "currently alive" reference used by RotA's own spawn-gate
/// check). We postfix it to broadcast a new entity spawn the first time a client creates one locally that isn't
/// already known to Nitrox. Because RotA's own gate already checks that static "is one alive" reference before
/// triggering a new spawn, once a networked instance exists in a client's scene (received via
/// <see cref="Entities.BroadcastEntitySpawnedByClient"/> from whichever client spawned it first), that same
/// check naturally stops their local trigger from firing again - no changes to RotA's own gating logic are needed.
/// <para/>
/// Known limitation: if two players independently satisfy RotA's spawn condition before either spawn has round-tripped
/// over the network, both may spawn and broadcast their own instance. Acceptable for now; would need a claimed/reserved
/// spawn handshake to close entirely.
/// </remarks>
public sealed class VoidGargSingleton_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly Type voidGargSingletonType = AccessTools.TypeByName("RotA.Mono.Singletons.VoidGargSingleton");
    private static readonly MethodInfo targetMethod = voidGargSingletonType != null ? AccessTools.Method(voidGargSingletonType, "Start") : null;
    private static readonly MethodInfo postfixMethod = AccessTools.Method(typeof(VoidGargSingleton_Start_Patch), nameof(Postfix));

    public override void Patch(Harmony harmony)
    {
        if (targetMethod == null)
        {
            // Return of the Ancients isn't installed - nothing to patch.
            return;
        }

        PatchPostfix(harmony, targetMethod, postfixMethod);
    }

    public static void Postfix(object __instance)
    {
        if (!Multiplayer.Active)
        {
            return;
        }

        GameObject gargObject = ((Component)__instance).gameObject;
        if (gargObject.GetComponent<NitroxEntity>())
        {
            // Already came from the network (received via BroadcastEntitySpawnedByClient from another client).
            return;
        }

        if (!gargObject.TryGetComponent(out TechTag techTag) || !gargObject.TryGetComponent(out PrefabIdentifier prefabIdentifier))
        {
            return;
        }

        int cellLevel = gargObject.TryGetComponent(out LargeWorldEntity largeWorldEntity) ? (int)largeWorldEntity.cellLevel : 100;

        NitroxId id = NitroxEntity.GenerateNewId(gargObject);
        WorldEntity entity = new(
            gargObject.transform.position.ToDto(),
            gargObject.transform.rotation.ToDto(),
            gargObject.transform.localScale.ToDto(),
            techTag.type.ToDto(),
            cellLevel,
            prefabIdentifier.ClassId,
            false,
            id,
            null);
        Resolve<Entities>().BroadcastEntitySpawnedByClient(entity);
    }
}
