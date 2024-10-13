using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using NineSolsAPI;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DpsMeter;

public class DpsDisplay(
    DpsTracker tracker,
    ConfigEntry<bool> configShowDamageNumbers,
    ConfigEntry<bool> updateOnHits,
    TMP_Text statsPanel) {
    private const float MoveAmount = 50f;
    private const float FadeDuration = 2f;
    private const float ScaleFactor = 1.2f;

    private List<TMP_Text> objects = [];

    // ReSharper disable Unity.PerformanceAnalysis
    public IEnumerator OnDamage(EffectHitData data, float value, bool internalDamage) {
        if (!configShowDamageNumbers.Value) yield break;

        var color = internalDamage ? new Color(0.5f, 1, 1, 1) : Color.white;
        yield return SpawnText(data.hitPos, $"{value:0.##}", color);
    }

    // ReSharper disable Unity.PerformanceAnalysis
    public IEnumerator OnInaccurateParry(EffectHitData data, float parryTime, float requiredParryTime,
        float spamLevel) {
        if (!configShowDamageNumbers.Value) yield break;

        var parryOvertime = parryTime - requiredParryTime;

        var msg = $"Missed by {parryOvertime * 1000:F0}ms";
        if (spamLevel > 0) {
            msg += $" (Spam {spamLevel})";
        }

        if (requiredParryTime == 0) {
            msg = "Parry Spam";
        }

        yield return SpawnText(data.hitPos,
            msg,
            new Color(1f, 0.0f, 0.28f, 1)
        );
    }

    private IEnumerator SpawnText(Vector3 pos, string str, Color color) {
        var text = CreateDamageText(str, color);
        objects.Add(text);
        yield return AnimateText(text, pos);

        objects.Remove(text);
    }


    public void Update() {
        var recentHits = tracker.RecentHits;
        if (recentHits.Count == 0) {
            statsPanel.text = "";
            return;
        }

        var sum = recentHits.Sum(hit => hit.Value);

        var totalDamage = recentHits.Sum(hit => hit.Value);
        var grouped = recentHits.GroupBy(hit => hit.Type,
                hit => hit.Value,
                (type, group) => {
                    var groupSum = group.Sum();
                    var percentage = (int)(groupSum / totalDamage * 100);
                    return (type, groupSum, percentage);
                })
            .OrderByDescending(x => x.groupSum)
            .Select(x => $"{x.type}: {x.groupSum:0.##} {x.percentage}%");

        statsPanel.text = grouped.Join(delimiter: "\n");

        float duration;
        if (updateOnHits.Value) {
            if (recentHits.Count > 0) {
                duration = recentHits.Last().Time - recentHits.First().Time;
            } else {
                duration = 0;
            }
        } else {
            duration = tracker.RunningTime;
        }

        if (duration != 0) {
            var dps = sum / duration;
            statsPanel.text += $"\nDPS: {dps:F1}";
        }
    }

    public void OnDestroy() {
        foreach (var obj in objects) {
            Object.Destroy(obj.gameObject);
        }
    }

    private static Vector2 WorldToCanvas(Vector3 worldPosition) {
        var canvas = NineSolsAPICore.FullscreenCanvas.transform;
        var cam = CameraManager.Instance.camera2D.GameCamera;

        var screenPosition = cam.WorldToScreenPoint(worldPosition);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas.GetComponent<RectTransform>(),
            screenPosition,
            null,
            out var uiPosition);

        return uiPosition;
    }


    private static TMP_Text CreateDamageText(string str, Color color) {
        var canvas = NineSolsAPICore.FullscreenCanvas.transform;

        var textGo = new GameObject();
        textGo.transform.SetParent(canvas);

        var text = (TMP_Text)textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 25f;
        text.alignment = TextAlignmentOptions.Bottom;
        text.fontWeight = FontWeight.Bold;
        text.text = str;
        text.color = color;

        var transform = text.GetComponent<RectTransform>();
        transform.pivot = new Vector2(0.5f, 0f);
        transform.sizeDelta = new Vector2(200f, 10f);

        return text;
    }


    private static IEnumerator AnimateText(TMP_Text text, Vector3 worldPosition) {
        var originalPosition = WorldToCanvas(worldPosition);

        var rectTransform = text.GetComponent<RectTransform>();
        rectTransform.anchoredPosition = originalPosition;

        var targetPositionWorld = worldPosition + new Vector3(0, MoveAmount, 0);

        var elapsedTime = 0f;
        var originalColor = text.color;
        var originalScale = rectTransform.localScale.x;

        while (elapsedTime < FadeDuration) {
            rectTransform.anchoredPosition =
                WorldToCanvas(Vector3.Lerp(worldPosition, targetPositionWorld, elapsedTime / FadeDuration));

            var newColor = text.color;
            newColor.a = Mathf.Lerp(1f, 0f, elapsedTime / FadeDuration);
            text.color = newColor;

            var scale = Mathf.Lerp(originalScale, ScaleFactor, elapsedTime / (FadeDuration / 2f));
            if (elapsedTime > FadeDuration / 2f) {
                scale = Mathf.Lerp(ScaleFactor, originalScale, (elapsedTime - FadeDuration / 2f) / (FadeDuration / 2f));
            }

            rectTransform.localScale = new Vector3(scale, scale, scale);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        text.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
        Object.Destroy(text.gameObject);
    }
}

internal static class Extensions {
    public static string Name(this EffectType type) {
        if ((type & EffectType.HeavyAttack) != 0) return "Heavy";
        if ((type & EffectType.FooEffect) != 0) return "Talisman Attach";
        if ((type & EffectType.FooExplode) != 0) return "Talisman Explode";
        if ((type & EffectType.JumpKickEffect) != 0) return "Tai-Chi";

        return type.ToString();
    }
}