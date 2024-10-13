using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DpsMeter;

public record struct HitData(float Time, float Value, string Type, GameObject Owner);

public class DpsTracker(ConfigEntry<DpsResetMode> resetMode) {
    public readonly List<HitData> RecentHits = [];

    public bool Running = true;
    public float RunningTime;

    private GameObject? lockedInToOwner = null;

    public void Pause() {
        Running = !Running;
        ToastManager.Toast($"DPS tracking enabled: {Running}");
    }

    public void Reset() {
        RecentHits.Clear();
        RunningTime = 0;
        lockedInToOwner = null;

        ToastManager.Toast($"DPS tracking reset");
    }

    public void Update() {
        if (Running) {
            RunningTime += Time.deltaTime;
        }
    }

    public void OnDamage(EffectHitData data, float value, bool internalDamage) {
        if (!Running) return;

        var owner = data.receiver.OwnerComponent.gameObject;

        switch (resetMode.Value) {
            case DpsResetMode.ManualResets:
                break;
            case DpsResetMode.ResetOnEnemyChange:
                if (RecentHits.Count > 0) {
                    var previousOwner = RecentHits.Last().Owner;

                    if (previousOwner != owner) {
                        Reset();
                        ToastManager.Toast("New enemy, resetting DPS stats");
                    }
                }

                break;
            case DpsResetMode.LockToFirstEnemy:
                if (lockedInToOwner is null)
                    lockedInToOwner = owner;
                else if (owner != lockedInToOwner) return;

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var name = data.dealer.name;
        if (name is "PostureDecrease" or "Posture Decrease") {
            name = data.dealer.transform.parent.name;
        }

        string? attackName = null;
        for (var dealer = data.dealer.transform; dealer; dealer = dealer.parent) {
            if (dealerNames.TryGetValue(dealer.name, out attackName)) {
                break;
            }
        }

        if (attackName is null) {
            Log.Warning(ObjectUtils.ObjectPath(data.dealer.gameObject));
            attackName = data.dealer.name;
        }

        RecentHits.Add(new HitData(Time.time, value, attackName, owner));
    }


    private readonly Dictionary<string, string> dealerNames = new() {
        { "AttackFront", "Attack" },
        { "ChargedAttackFront", "Charge Attack" },
        { "Third Attack", "Heavy" },
        { "Foo", "Foo Attach" },
        { "FooExplode", "Foo" },
        { "JumpSpinKick", "Tai Chi" },
        { "[Dealer] Internal Damage", "UC" },
        { "--ReflectNode", "Reflect Projectile" },
        { "NormalArrow Shoot 穿雲 Lv1(Clone)", "Bow" },
        { "NormalArrow Shoot 穿雲 Lv2(Clone)", "Bow" },
        { "NormalArrow Shoot 穿雲 Lv3(Clone)", "Bow" },
        { "[Jade]AccurateParryReflect", "Hedgehog Jade" },
    };
}