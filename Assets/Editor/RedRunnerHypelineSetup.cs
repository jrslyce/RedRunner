using System;
using System.Collections.Generic;
using System.Reflection;
using MuxyHypeline;
using RedRunner;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

// Reproducible edit-mode setup for RedRunner's binary Play scene.
public static class RedRunnerHypelineSetup
{
    private const string ScenePath = "Assets/Scenes/Play.unity";
    private const string PrefabPath = "Packages/com.muxy.gateway/Prefabs/";

    [MenuItem("Tools/RedRunner/Set up Hypeline")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Leave Play mode before configuring Hypeline.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        MuxyHypelineManager manager = GetOrCreate<MuxyHypelineManager>("MuxyHypelineManager");
        MuxyHypelineMenu menu = GetOrCreate<MuxyHypelineMenu>("MuxyHypelineMenu");
        MuxyHypelineInteractionAlert alert = GetOrCreate<MuxyHypelineInteractionAlert>("MuxyHypelineInteractionAlert");

        var demoObject = GameObject.Find("HypelineActionDemo") ?? new GameObject("HypelineActionDemo");
        var demo = demoObject.GetComponent<HypelineActionDemo>() ?? demoObject.AddComponent<HypelineActionDemo>();
        var interactionsObject = GameObject.Find("RedRunnerHypelineInteractions") ?? new GameObject("RedRunnerHypelineInteractions");
        var interactions = interactionsObject.GetComponent<RedRunnerHypelineInteractions>() ??
                           interactionsObject.AddComponent<RedRunnerHypelineInteractions>();

        manager.GameID = "gamelink-testing";
        var metadataField = typeof(MuxyHypelineManager).GetField("GameMetadata", BindingFlags.Instance | BindingFlags.NonPublic);
        var metadata = (GameMetadata)metadataField.GetValue(manager);
        metadata.Name = "Red Runner";
        metadata.Theme = "";
        metadataField.SetValue(manager, metadata);

        menu.HypelineManager = manager;
        alert.HypelineManager = manager;
        demo.HypelineManager = manager;
        interactions.HypelineManager = manager;
        alert.gameObject.SetActive(true);
        alert.Position = new Vector2(-32, -140);
        menu.MenuButtonPosition = new Vector2(24, -24);
        menu.MenuButtonSize = new Vector2(280, 68);

        var actionsField = typeof(MuxyHypelineManager).GetField("GameActions", BindingFlags.Instance | BindingFlags.NonPublic);
        var actions = (List<GameAction>)actionsField.GetValue(manager);
        if (actions.Count == 0)
        {
            actions = new List<GameAction>
            {
                MakeAction("healthpack", "Spawn Healthpack", "Add up to 10 health to the player.",
                    GameActionCategory.Help, "fa-solid:heart", demo.OnHealthpack),
                MakeAction("giftcoins", "Gift 10 Coins", "Give Red 10 coins.",
                    GameActionCategory.Help, "fa-solid:coins", interactions.OnGameAction),
                MakeAction("jumpboost", "Boost Jump", "Boost Red's jump for 15 seconds.",
                    GameActionCategory.Help, "fa-solid:arrow-up", interactions.OnGameAction),
                MakeAction("slowrunner", "Slow Runner", "Slow Red for 15 seconds.",
                    GameActionCategory.Hinder, "fa-solid:snail", interactions.OnGameAction),
                MakeAction("heavyboots", "Heavy Boots", "Lower Red's jump for 15 seconds.",
                    GameActionCategory.Hinder, "fa-solid:weight-hanging", interactions.OnGameAction),
                MakeAction("cointax", "Coin Tax", "Remove up to 5 coins.",
                    GameActionCategory.Hinder, "fa-solid:coins", interactions.OnGameAction),
                MakeAction("cointoss", "Coin Toss", "Randomly add 10 or remove up to 5 coins.",
                    GameActionCategory.Neutral, "fa-solid:coins", interactions.OnGameAction),
                MakeAction("jumproulette", "Jump Roulette", "Randomly raise or lower Red's jump for 15 seconds.",
                    GameActionCategory.Neutral, "fa-solid:arrows-up-down", interactions.OnGameAction),
                MakeAction("paceroulette", "Pace Roulette", "Randomly speed up or slow Red for 15 seconds.",
                    GameActionCategory.Neutral, "fa-solid:shuffle", interactions.OnGameAction)
            };
            actionsField.SetValue(manager, actions);
        }
        else if (actions.Count != 9) throw new InvalidOperationException("Existing Hypeline actions need manual review.");

        var textsField = typeof(MuxyHypelineManager).GetField("GameTexts", BindingFlags.Instance | BindingFlags.NonPublic);
        var texts = (List<GameText>)textsField.GetValue(manager);
        if (texts.Count == 0)
        {
            texts = new List<GameText>
            {
                new GameText { Label = "Health", Value = "50", Icon = "fa-solid:heart" },
                new GameText { Label = "Coins", Value = "0", Icon = "fa-solid:coins" },
                new GameText { Label = "Distance", Value = "0", Icon = "fa-solid:person-running" }
            };
            textsField.SetValue(manager, texts);
        }
        else if (texts.Count != 3) throw new InvalidOperationException("Existing Hypeline texts need manual review.");

        var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (eventSystems.Length == 0)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        else if (eventSystems.Length != 1)
            throw new InvalidOperationException("Play scene has more than one EventSystem.");

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(alert);
        EditorUtility.SetDirty(demo);
        EditorUtility.SetDirty(interactions);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("RedRunner Hypeline setup saved: 9 actions, 3 texts, one manager and one alert.");
    }

    private static T GetOrCreate<T>(string prefabName) where T : Component
    {
        var matches = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (matches.Length > 1) throw new InvalidOperationException("Duplicate " + typeof(T).Name + " components found.");
        if (matches.Length == 1) return matches[0];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + prefabName + ".prefab");
        if (prefab == null) throw new InvalidOperationException("Hypeline prefab missing: " + prefabName);
        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null) throw new InvalidOperationException("Could not instantiate " + prefabName);
        return instance.GetComponent<T>();
    }

    private static GameAction MakeAction(string id, string name, string description,
        GameActionCategory category, string icon, UnityAction<GameActionUsed> callback)
    {
        var action = new GameAction
        {
            ID = id,
            Name = name,
            Description = description,
            Category = category,
            State = GameActionState.Available,
            Impact = 1,
            Icon = icon,
            Count = 65535,
            OnGameActionUsed = new MuxyHypelineGameActionUsedEvent()
        };
        UnityEventTools.AddPersistentListener(action.OnGameActionUsed, callback);
        return action;
    }
}
