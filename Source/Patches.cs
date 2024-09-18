using System.Collections.Generic;
using HarmonyLib;
using InControl;
using UnityEngine;

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


    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.HittedByPlayerDecreasePosture))]
    [HarmonyPrefix]
    public static void OnDamage(EffectHitData hitData) {
        DpsMeterMod.Instance.OnDamage(hitData, hitData.dealer.FinalValue, false);
    }

    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.InternalInjuryVirtual))]
    [HarmonyPrefix]
    public static void OnDamageInternal(EffectHitData data, float scale, float additionalDamage) {
        var num = (float)(int)(data.dealer.FinalValue * scale + additionalDamage);
        DpsMeterMod.Instance.OnDamage(data, num, true);
    }
}