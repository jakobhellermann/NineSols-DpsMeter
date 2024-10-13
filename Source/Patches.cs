using System.Collections.Generic;
using HarmonyLib;
using InControl;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace DpsMeter;

[HarmonyPatch]
public class Patches {
    private static AccessTools.FieldRef<PlayerInputCommandQueue, Dictionary<PlayerAction, List<float>>> actionDict =
        AccessTools.FieldRefAccess<PlayerInputCommandQueue, Dictionary<PlayerAction, List<float>>>("actionDict");

    private static AccessTools.FieldRef<PlayerParryState, float[]> spamDatas =
        AccessTools.FieldRefAccess<PlayerParryState, float[]>("spamDatas");

    [HarmonyPatch(typeof(PlayerParryState), nameof(PlayerParryState.Parried))]
    [HarmonyPrefix]
    public static void Parried(ref PlayerParryState __instance, EffectHitData hitData) {
        var player = Player.i;

        var parryTimeList = actionDict.Invoke(player.inputCommandQueue)[player.playerInput.gameplayActions.Parry];
        var num = parryTimeList.Count > 0 ? parryTimeList[^1] : -1;


        var spamLevels = spamDatas.Invoke(__instance);
        var spamLevel = __instance.spamLevel;

        var num5 = Time.time - num;
        var isAccurate = __instance.IsAlwaysAccurate || player.IsInQTETutorial ||
                         num5 < spamLevels[spamLevel];

        if (!isAccurate) {
            DpsMeterMod.Instance.OnInaccurateParry(hitData,
                num5,
                spamLevels[spamLevel],
                spamLevel);
        }
    }

    public record struct CurrentHealth(float HealthValue, float InternalInjury) {
        public float TotalHealth => HealthValue + InternalInjury;

        internal static CurrentHealth From(PostureSystem health) {
            return new CurrentHealth(health.CurrentHealthValue, health.CurrentInternalInjury);
        }
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.HittedByPlayerDecreasePosture))]
    [HarmonyPrefix]
    private static void OnDamagePrefix(MonsterBase __instance, out CurrentHealth __state) {
        __state = CurrentHealth.From(__instance.postureSystem);
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.HittedByPlayerDecreasePosture))]
    [HarmonyPostfix]
    private static void OnDamage(MonsterBase __instance, CurrentHealth __state, EffectHitData hitData) {
        var newHealth = CurrentHealth.From(__instance.postureSystem);
        DpsMeterMod.Instance.OnDamage(hitData, __state, newHealth);
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.InternalInjuryVirtual))]
    [HarmonyPrefix]
    private static void OnDamageInternalPrefix(MonsterBase __instance, out CurrentHealth __state) {
        __state = CurrentHealth.From(__instance.postureSystem);
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.InternalInjuryVirtual))]
    [HarmonyPrefix]
    private static void OnDamageInternal(MonsterBase __instance, CurrentHealth __state, EffectHitData data) {
        var newHealth = CurrentHealth.From(__instance.postureSystem);
        DpsMeterMod.Instance.OnDamage(data, __state, newHealth);
    }
}