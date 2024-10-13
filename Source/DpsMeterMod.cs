using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NineSolsAPI;
using NineSolsAPI.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DpsMeter;

public enum DpsResetMode {
    ManualResets,
    ResetOnEnemyChange,
    LockToFirstEnemy,
}

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class DpsMeterMod : BaseUnityPlugin {
    private ConfigEntry<DpsResetMode> configResetMode = null!;
    private ConfigEntry<bool> configUpdateOnHits = null!;
    private ConfigEntry<bool> configShowDamageNumbers = null!;

    // private ConfigEntry<KeyboardShortcut> configShortcutSpawnTrainingDummy = null!;
    private ConfigEntry<KeyboardShortcut> configShortcutPause = null!;
    private ConfigEntry<KeyboardShortcut> configShortcutReset = null!;

    public static DpsMeterMod Instance = null!;
    private Harmony harmony = null!;

    private DpsTracker dpsTracker = null!;
    private DpsDisplay dpsDisplay = null!;

    private GameObject dummy = null!;
    public TMP_Text statsPanel = null!;


    private void Awake() {
        Instance = this;
        Log.Init(Logger);
        RCGLifeCycle.DontDestroyForever(gameObject);

        try {
            configResetMode = Config.Bind("General", "Reset Mode", DpsResetMode.ResetOnEnemyChange,
                "When to reset the DPS tracking.\n" +
                "Manual Resets = You have to hit the reset shortcut manually\n" +
                "Lock To First Enemy = After hitting an enemy, only damage to that enemy will be tracked (until manually reset)\n" +
                "Reset On Enemy Change = Switching the enemy automatically resets the DPS tracking");
            configUpdateOnHits = Config.Bind("General", "Only update DPS on hits", true);
            configShowDamageNumbers = Config.Bind("General", "Show damage numbers", true);

            /*configShortcutSpawnTrainingDummy = Config.Bind("Shortcuts",
                "Spawn training dummy",
                new KeyboardShortcut(KeyCode.T, KeyCode.LeftControl));*/
            configShortcutPause = Config.Bind("Shortcuts", "Pause DPS Tracking", KeyboardShortcut.Empty);
            configShortcutReset = Config.Bind("Shortcuts", "Reset DPS Tracking", KeyboardShortcut.Empty);

            statsPanel = CreateStatsPanel();

            harmony = Harmony.CreateAndPatchAll(typeof(DpsMeterMod).Assembly);
            dpsTracker = new DpsTracker(configResetMode);
            dpsDisplay = new DpsDisplay(dpsTracker, configShowDamageNumbers, configUpdateOnHits, statsPanel);

            // KeybindManager.Add(this, SpawnTrainingDummy, () => configShortcutSpawnTrainingDummy.Value);
            KeybindManager.Add(this, () => dpsTracker.Pause(), () => configShortcutPause.Value);
            KeybindManager.Add(this, () => dpsTracker.Reset(), () => configShortcutReset.Value);

            KeybindManager.Add(this, () => GameCore.Instance?.ResetLevel(), KeyCode.T);

            /*dummy = LoadObjectFromResources(
                "DpsMeter.preloads.bundle",
                "Assets/SceneBundle/A0_S7_CaveReturned.unity",
                "StealthGameMonster_TutorialDummyNonAttack"
            );*/

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    private static TMP_Text CreateStatsPanel() {
        var statsTextObj = new GameObject("Stats");
        statsTextObj.transform.SetParent(NineSolsAPICore.FullscreenCanvas.transform);
        var statsText = statsTextObj.AddComponent<TextMeshProUGUI>();
        statsText.alignment = TextAlignmentOptions.BottomRight;
        statsText.fontSize = 20;
        statsText.color = Color.white;
        statsText.text = "";

        var statsTextTransform = statsText.GetComponent<RectTransform>();
        statsTextTransform.anchorMin = new Vector2(1, 0.5f);
        statsTextTransform.anchorMax = new Vector2(1, 0.5f);
        statsTextTransform.pivot = new Vector2(1f, 0f);
        statsTextTransform.anchoredPosition = new Vector2(-10, 10);
        statsTextTransform.sizeDelta = new Vector2(Screen.width, 0f);

        return statsText;
    }

    private void Update() {
        dpsTracker.Update();
        dpsDisplay.Update();
    }

    private void SpawnTrainingDummy() {
        if (Player.i is not { } player) return;

        var dummyCopy = Instantiate(dummy, Instance.gameObject.transform);
        dummyCopy.transform.position = player.transform.position;
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        dpsDisplay.OnDestroy();

        if (dummy) Destroy(dummy);
        if (statsPanel) Destroy(statsPanel.gameObject);
    }

    public void OnDamage(
        EffectHitData hitData,
        Patches.CurrentHealth before,
        Patches.CurrentHealth after
    ) {
        var healthDamage = before.HealthValue - after.HealthValue;

        var onlyInternalDamage = after.InternalInjury - before.InternalInjury;
        var onlyProccedDamage = before.TotalHealth - after.TotalHealth;

        // if (healthDamage > 0) StartCoroutine(dpsDisplay.OnDamage(hitData, healthDamage, false));
        if (onlyProccedDamage > 0) StartCoroutine(dpsDisplay.OnDamage(hitData, onlyProccedDamage, false));
        if (onlyInternalDamage > 0) StartCoroutine(dpsDisplay.OnDamage(hitData, onlyInternalDamage, true));

        dpsTracker.OnDamage(hitData, healthDamage);
    }

    public void OnInaccurateParry(EffectHitData hitData, float parryTime, float requiredParryTime, float spamLevel) {
        StartCoroutine(dpsDisplay.OnInaccurateParry(hitData, parryTime, requiredParryTime, spamLevel));
    }

    private static GameObject LoadObjectFromResources(string resourceName, string scenePath, string objectName) {
        GameObject? copy = null;

        var bundle = AssemblyUtils.GetEmbeddedAssetBundle(resourceName)!;
        SceneManager.LoadScene(scenePath, LoadSceneMode.Additive);
        var scene = SceneManager.GetSceneByPath(scenePath);
        foreach (var obj in scene.GetRootGameObjects()) {
            if (obj.name == objectName) {
                copy = Instantiate(obj);
                copy.name = "Training Dummy";
                RCGLifeCycle.DontDestroyForever(copy);
                break;
            }

            Destroy(obj);
        }

        SceneManager.UnloadSceneAsync(scene);
        bundle.Unload(true);

        if (copy is null) {
            ToastManager.Toast(bundle);
            ToastManager.Toast(scene);
            ToastManager.Toast(scene.GetRootGameObjects().Length);
            throw new Exception($"{objectName} not found in {resourceName}/{scenePath}");
        }

        return copy;
    }
}