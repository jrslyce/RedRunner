using System.Collections.Generic;
using MuxyHypeline;
using RedRunner;
using UnityEngine;

// Tutorial scene only: use the prefab manager as the sole SDK owner.
public class HypelineActionDemo : MonoBehaviour
{
    public MuxyHypelineManager HypelineManager;
    private MuxyHypelineMenu menu;
    private int health = 50;
    private static readonly HashSet<string> fulfilledTransactions = new HashSet<string>();
    private string status = "Open Stream Interactivity and connect with your Twitch code.";

    private void OnEnable()
    {
        if (HypelineManager == null)
        {
            Debug.LogError("Assign Hypeline Manager before enabling the demo.");
            enabled = false;
            return;
        }
        HypelineManager.OnAuthentication.AddListener(OnAuthentication);
    }

    private void OnDisable()
    {
        if (HypelineManager != null)
            HypelineManager.OnAuthentication.RemoveListener(OnAuthentication);
    }

    private void OnAuthentication(AuthenticationResponse response)
    {
        status = response.HasError ? "Authentication failed. Request a fresh PIN."
                                  : "Authenticated. Try Spawn Healthpack in the extension.";
        if (!response.HasError) PublishHealth();
    }

    // Wire the action's Dynamic GameActionUsed event to this public method.
    public void OnHealthpack(GameActionUsed used)
    {
        if (used == null || used.ActionID != "healthpack") return;
        if (string.IsNullOrEmpty(used.TransactionID))
        {
            HypelineManager.RefundGameAction(used, "Missing transaction ID.");
            return;
        }
        if (fulfilledTransactions.Contains(used.TransactionID))
        {
            HypelineManager.AcceptGameAction(used, "Already fulfilled.");
            return;
        }
        if (GameManager.Singleton == null || !GameManager.Singleton.gameRunning ||
            !GameManager.Singleton.gameStarted)
        {
            HypelineManager.RefundGameAction(used, "The run is not active.");
            return;
        }
        ApplyHealth();
        fulfilledTransactions.Add(used.TransactionID);
        HypelineManager.AcceptGameAction(used, "Added up to 10 health.");
        status = "Viewer action received: healthpack.";
        Debug.Log("Hypeline viewer action received: healthpack.");
    }

    private void ApplyHealth()
    {
        health = Mathf.Min(100, health + 10);
        if (HypelineManager.IsAuthenticated()) PublishHealth();
    }

    private void PublishHealth()
    {
        // The guide creates Health as Game Texts element 0.
        GameText text = HypelineManager.GetGameTextAt(0);
        text.Value = health.ToString();
        HypelineManager.SetGameTextAt(text, 0);
    }

    private bool ShouldShowControls()
    {
        if (menu == null) menu = FindFirstObjectByType<MuxyHypelineMenu>();
        return menu == null || !menu.IsPanelOpen;
    }

    private void OnGUI()
    {
        if (!ShouldShowControls()) return;
        GUILayout.BeginArea(new Rect(16, Screen.height - 156, 440, 140), GUI.skin.box);
        GUILayout.Label("Hypeline action test | Health: " + health);
        GUILayout.Label(status);
        if (GUILayout.Button("Local test: add 10 health")) ApplyHealth();
        GUILayout.EndArea();
    }
}
