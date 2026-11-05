using ReplantedModUi;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DebugMod;

// Own a presentation copy of the actual Mini-Games panel, grid and cards.
// Native profile/transition binders never run on these copies.
internal sealed class DebugLevelPicker : IDisposable
{
    internal sealed record Entry(string Name, Func<Sprite?> Thumbnail, Action Select);
    private readonly GameObject staging, root;
    private readonly List<(Image Image, Func<Sprite?> Thumbnail)> thumbnails = new();

    internal DebugLevelPicker(Entry[] entries, Action cancel, string error)
    {
        const string panelPath = "Canvas/Layout/Center/Panel";
        var template = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(obj =>
            obj.name.StartsWith("P_ChallengePanel_Mini-Games", StringComparison.Ordinal)
            && obj.transform.Find(panelPath) != null)
            ?? throw new InvalidOperationException("Native Mini-Games panel template is unavailable.");
        staging = new GameObject("FusionDebugPickerStaging"); staging.SetActive(false);
        root = NativeUi.Clone(template, staging.transform, staging.transform, "FusionDebugMiniGames");
        try
        {
            var canvas = root.transform.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            foreach (var group in root.GetComponentsInChildren<CanvasGroup>(true))
            { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }
            var panel = root.transform.Find(panelPath);
            panel.Find("HeaderText").GetComponent<TMP_Text>().text = string.IsNullOrEmpty(error) ? "Debug · Background wählen" : $"Debug: {error}";
            panel.Find("TrophyCounter").gameObject.SetActive(false);
            root.transform.Find("Canvas/Layout/Center/P_ControllerPrompt_Legend").gameObject.SetActive(false);
            var grid = panel.Find("Scroll View/ScrollViewport/GridLayout");
            var card = grid.Find("P_ChallengePanel_ChallengeItem").gameObject;
            for (int i = 0; i < grid.childCount; ++i) grid.GetChild(i).gameObject.SetActive(false);
            Button? first = null;
            foreach (var entry in entries)
            {
                var clone = NativeUi.Clone(card, staging.transform, grid, "FusionDebugBackground");
                var show = clone.transform.Find("Show_Challenge");
                show.gameObject.SetActive(true);
                foreach (var name in new[] { "ChallengeThumbnail_Locked", "ChallengeThumbnail_UnLocked",
                    "Background_Locked", "TrophyFlag", "StreakText", "ChallengeDisabledNameText " })
                    show.Find(name)?.gameObject.SetActive(false);
                show.Find("ChallengeNameText").GetComponent<TMP_Text>().text = entry.Name;
                var image = show.Find("ChallengeThumbnail").GetComponent<Image>();
                image.gameObject.SetActive(true); image.preserveAspect = true;
                image.sprite = entry.Thumbnail();
                thumbnails.Add((image, entry.Thumbnail));
                var button = clone.GetComponent<Button>();
                button.enabled = button.interactable = true;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener((UnityAction)(() => entry.Select()));
                var navigation = button.navigation; navigation.mode = Navigation.Mode.Automatic; button.navigation = navigation;
                first ??= button;
                clone.SetActive(true);
            }
            var back = panel.Find("P_PanelButton").GetComponent<Button>();
            back.onClick = new Button.ButtonClickedEvent(); back.onClick.AddListener((UnityAction)(() => cancel()));
            back.interactable = true;
            root.transform.SetParent(null, false);
            root.SetActive(true);
            var scroll = panel.Find("Scroll View").GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 1;
            if (first != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        catch { Dispose(); throw; }
    }

    internal void Update()
    {
        foreach (var (image, get) in thumbnails)
        {
            var sprite = get();
            if (image.sprite != sprite) image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }

    public void Dispose()
    {
        if (root) { root.SetActive(false); Object.Destroy(root); }
        if (staging) Object.Destroy(staging);
        thumbnails.Clear();
    }
}
