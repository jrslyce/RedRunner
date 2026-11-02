using System;
using System.Collections.Generic;
using MuxyHypeline;
using RedRunner.Characters;
using UnityEngine;

namespace RedRunner
{
    // The prefab manager is the only SDK owner. This component owns eight
    // RedRunner actions; the imported tutorial component owns healthpack.
    public sealed class RedRunnerHypelineInteractions : MonoBehaviour
    {
        public MuxyHypelineManager HypelineManager;

        private static readonly HashSet<string> FulfilledTransactions = new HashSet<string>();
        private bool? actionsAvailable;
        private float nextRefreshAt;
        private string lastCoins;
        private string lastDistance;

        private bool CanFulfill
        {
            get
            {
                GameManager game = GameManager.Singleton;
                return HypelineManager != null && HypelineManager.IsAuthenticated() &&
                       game != null && game.gameStarted && game.gameRunning &&
                       game.MainCharacter != null && !game.MainCharacter.IsDead.Value;
            }
        }

        private void OnEnable()
        {
            if (HypelineManager == null)
            {
                Debug.LogError("Assign Hypeline Manager to RedRunnerHypelineInteractions.", this);
                enabled = false;
                return;
            }
            HypelineManager.OnAuthentication.AddListener(OnAuthentication);
        }

        private void Start()
        {
            var alert = FindFirstObjectByType<MuxyHypelineInteractionAlert>();
            var canvas = alert != null ? alert.GetComponent<Canvas>() : null;
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
            }
        }

        private void OnDisable()
        {
            if (HypelineManager != null)
                HypelineManager.OnAuthentication.RemoveListener(OnAuthentication);
        }

        private void OnAuthentication(AuthenticationResponse response)
        {
            if (response.HasError) return;
            actionsAvailable = null;
            lastCoins = lastDistance = null;
            nextRefreshAt = 0f;
        }

        private void Update()
        {
            if (HypelineManager == null || !HypelineManager.IsAuthenticated()) return;

            bool available = CanFulfill;
            if (actionsAvailable != available)
            {
                HypelineManager.SetAllGameActionStates(available ? GameActionState.Available : GameActionState.Unavailable);
                actionsAvailable = available;
            }

            if (Time.realtimeSinceStartup < nextRefreshAt || GameManager.Singleton == null) return;
            nextRefreshAt = Time.realtimeSinceStartup + 0.5f;

            string coins = GameManager.Singleton.m_Coin.Value.ToString();
            string distance = Mathf.FloorToInt(GameManager.Singleton.Score).ToString();
            if (coins != lastCoins)
            {
                SetText(1, coins);
                lastCoins = coins;
            }
            if (distance != lastDistance)
            {
                SetText(2, distance);
                lastDistance = distance;
            }
        }

        private void SetText(int index, string value)
        {
            GameText text = HypelineManager.GetGameTextAt(index);
            text.Value = value;
            HypelineManager.SetGameTextAt(text, index);
        }

        // Bound once to each of the eight RedRunner actions' dynamic events.
        public void OnGameAction(GameActionUsed used)
        {
            if (used == null) return;
            switch (used.ActionID)
            {
                case "giftcoins":
                    Fulfill(used, () => AddCoins(10), "Gave Red 10 coins.");
                    break;
                case "jumpboost":
                    Fulfill(used, () => Runner().ApplyJumpBoost(1.5f, 15f), "Boosted Red's jump for 15 seconds.");
                    break;
                case "slowrunner":
                    Fulfill(used, () => Runner().ApplySpeedBoost(0.8f, 15f), "Slowed Red for 15 seconds.");
                    break;
                case "heavyboots":
                    Fulfill(used, () => Runner().ApplyJumpBoost(0.7f, 15f), "Lowered Red's jump for 15 seconds.");
                    break;
                case "cointax":
                    Fulfill(used, () => AddCoins(-5), "Removed up to 5 coins.");
                    break;
                case "cointoss":
                    Fulfill(used, () => AddCoins(UnityEngine.Random.value < 0.5f ? 10 : -5), "Flipped the coin outcome.");
                    break;
                case "jumproulette":
                    Fulfill(used, () => Runner().ApplyJumpBoost(UnityEngine.Random.value < 0.5f ? 0.7f : 1.5f, 15f),
                        "Rolled a jump modifier for 15 seconds.");
                    break;
                case "paceroulette":
                    Fulfill(used, () => Runner().ApplySpeedBoost(UnityEngine.Random.value < 0.5f ? 0.8f : 1.25f, 15f),
                        "Rolled a pace modifier for 15 seconds.");
                    break;
            }
        }

        private RedCharacter Runner()
        {
            RedCharacter runner = GameManager.Singleton.MainCharacter as RedCharacter;
            if (runner == null) throw new InvalidOperationException("The runner is unavailable.");
            return runner;
        }

        private void AddCoins(int amount)
        {
            GameManager game = GameManager.Singleton;
            game.m_Coin.Value = Mathf.Max(0, game.m_Coin.Value + amount);
        }

        private void Fulfill(GameActionUsed used, Action effect, string receipt)
        {
            if (string.IsNullOrEmpty(used.TransactionID))
            {
                HypelineManager.RefundGameAction(used, "Missing transaction ID.");
                return;
            }
            if (FulfilledTransactions.Contains(used.TransactionID))
            {
                HypelineManager.AcceptGameAction(used, "Already fulfilled.");
                return;
            }
            if (!CanFulfill)
            {
                HypelineManager.RefundGameAction(used, "The run is not active.");
                return;
            }

            try
            {
                effect();
                FulfilledTransactions.Add(used.TransactionID);
                HypelineManager.AcceptGameAction(used, receipt);
                Debug.Log("Hypeline RedRunner action fulfilled: " + used.ActionID, this);
            }
            catch (Exception exception)
            {
                // Once the effect ran, do not refund: the outcome is uncertain.
                if (FulfilledTransactions.Contains(used.TransactionID))
                    Debug.LogError("Hypeline fulfillment acknowledgment failed: " + exception.Message, this);
                else
                {
                    Debug.LogError("Hypeline gameplay effect failed: " + exception.Message, this);
                    HypelineManager.RefundGameAction(used, "The gameplay effect could not be applied.");
                }
            }
        }

    }
}
