/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardGameDef;
using CardGameDef.Unity;
using Cgs.CardGameView;
using Cgs.CardGameView.Multiplayer;
using Mirror;
using UnityEngine;

namespace Cgs.Play.Multiplayer
{
    public class CgsNetPlayer : NetworkBehaviour
    {
        public const string GameSelectionErrorMessage = "The host has selected a game that is not available!";
        public const string ShareDeckRequest = "Would you like to share the host's deck?";

        [field: SyncVar] public string Name { get; private set; }
        [field: SyncVar] public int Points { get; private set; }

        [field: SyncVar] public GameObject CurrentDeck { get; private set; }
        [field: SyncVar] public bool IsDeckShared { get; private set; }

        #region StartGame

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            Debug.Log("[CgsNet Player] Starting local player...");
            CgsNetManager.Instance.LocalPlayer = this;
            RequestNameUpdate(PlayerPrefs.GetString(Scoreboard.PlayerNamePlayerPrefs, Scoreboard.DefaultPlayerName));
            if (isServer)
                CgsNetManager.Instance.playController.ShowDeckMenu();
            else
                RequestCardGameSelection();
            Debug.Log("[CgsNet Player] Started local player!");
        }

        private void RequestCardGameSelection()
        {
            Debug.Log("[CgsNet Player] Requesting game id...");
            CmdSelectCardGame();
        }

        [Command]
        private void CmdSelectCardGame()
        {
            Debug.Log("[CgsNet Player] Sending game id...");
            Points = CardGameManager.Current.GameStartPointsCount;
            TargetSelectCardGame(CardGameManager.Current.Id, CardGameManager.Current.AutoUpdateUrl?.OriginalString);
        }

        [TargetRpc]
        private void TargetSelectCardGame(string gameId, string autoUpdateUrl)
        {
            Debug.Log($"[CgsNet Player] Game id is {gameId}! Loading game details...");
            if (!CardGameManager.Instance.AllCardGames.ContainsKey(gameId))
            {
                if (!Uri.IsWellFormedUriString(autoUpdateUrl, UriKind.Absolute))
                {
                    Debug.LogError(GameSelectionErrorMessage);
                    CardGameManager.Instance.Messenger.Show();
                    return;
                }

                StartCoroutine(DownloadGame(autoUpdateUrl));
            }
            else
            {
                CardGameManager.Instance.Select(gameId);
                StartCoroutine(WaitToStartGame());
            }
        }

        private IEnumerator DownloadGame(string url)
        {
            Debug.Log($"[CgsNet Player] Downloading game from {url}...");
            yield return CardGameManager.Instance.GetCardGame(url);
            yield return WaitToStartGame();
        }

        private IEnumerator WaitToStartGame()
        {
            while (CardGameManager.Current.IsDownloading)
                yield return null;

            Debug.Log("[CgsNet Player] Game loaded and ready!");

            switch (CardGameManager.Current.DeckSharePreference)
            {
                case SharePreference.Individual:
                    CgsNetManager.Instance.playController.ShowDeckMenu();
                    break;
                case SharePreference.Share:
                    RequestSharedDeck();
                    break;
                case SharePreference.Ask:
                default:
                    CardGameManager.Instance.Messenger.Ask(ShareDeckRequest,
                        CgsNetManager.Instance.playController.ShowDeckMenu, RequestSharedDeck);
                    break;
            }
        }

        #endregion

        #region Score

        public void RequestNameUpdate(string playerName)
        {
            CmdUpdateName(playerName);
        }

        [Command]
        private void CmdUpdateName(string playerName)
        {
            Name = playerName;
        }

        public void RequestPointsUpdate(int points)
        {
            CmdUpdatePoints(points);
        }

        [Command]
        private void CmdUpdatePoints(int points)
        {
            Points = points;
        }

        #endregion

        #region CardStacks

        public void RequestNewDeck(string deckName, IEnumerable<UnityCard> cards)
        {
            Debug.Log($"[CgsNet Player] Requesting new deck {deckName}...");
            CmdCreateCardStack(deckName, cards.Select(card => card.Id).ToArray(), true,
                CgsNetManager.Instance.playController.NextDeckPosition);
        }

        public void RequestNewCardStack(string stackName, IEnumerable<UnityCard> cards, Vector2 position)
        {
            Debug.Log($"[CgsNet Player] Requesting new card stack {stackName}...");
            CmdCreateCardStack(stackName, cards.Select(card => card.Id).ToArray(), false, position);
        }

        [Command]
        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        private void CmdCreateCardStack(string stackName, string[] cardIds, bool isDeck, Vector2 position)
        {
            Debug.Log($"[CgsNet Player] Creating new card stack {stackName}...");
            CardStack stack = CgsNetManager.Instance.playController.CreateCardStack(stackName,
                cardIds.Select(cardId => CardGameManager.Current.Cards[cardId]).ToList(), position);
            GameObject stackGameObject = stack.gameObject;
            NetworkServer.Spawn(stackGameObject);
            if (isDeck)
                CurrentDeck = stackGameObject;
            Debug.Log($"[CgsNet Player] Created new card stack {stackName}!");
        }

        private void RequestSharedDeck()
        {
            Debug.Log("[CgsNet Player] Requesting shared deck..");
            CmdShareDeck();
        }

        [Command]
        private void CmdShareDeck()
        {
            Debug.Log("[CgsNet Player] Sending shared deck...");
            TargetShareDeck(CgsNetManager.Instance.LocalPlayer.CurrentDeck);
        }

        [TargetRpc]
        private void TargetShareDeck(GameObject deckStack)
        {
            Debug.Log("[CgsNet Player] Received shared deck!");
            CurrentDeck = deckStack;
            IsDeckShared = true;
            CgsNetManager.Instance.playController.PromptForHand();
        }

        public void RequestShuffle(GameObject toShuffle)
        {
            Debug.Log("[CgsNet Player] Requesting shuffle...");
            CmdShuffle(toShuffle);
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdShuffle(GameObject toShuffle)
        {
            Debug.Log("[CgsNet Player] Shuffling!");
            var cardStack = toShuffle.GetComponent<CardStack>();
            cardStack.DoShuffle();
        }

        public void RequestInsert(GameObject stack, int index, string cardId)
        {
            Debug.Log($"[CgsNet Player] Requesting insert {cardId} at {index}...");
            CmdInsert(stack, index, cardId);
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdInsert(GameObject stack, int index, string cardId)
        {
            Debug.Log($"[CgsNet Player] Insert {cardId} at {index}!");
            var cardStack = stack.GetComponent<CardStack>();
            cardStack.Insert(index, cardId);
        }

        public void RequestRemoveAt(GameObject stack, int index)
        {
            Debug.Log($"[CgsNet Player] Requesting remove at {index}...");
            CmdRemoveAt(stack, index);
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdRemoveAt(GameObject stack, int index)
        {
            Debug.Log($"[CgsNet Player] Remove at {index}!");
            var cardStack = stack.GetComponent<CardStack>();
            cardStack.RemoveAt(index);
        }

        public void RequestDeal(GameObject stack, int count)
        {
            Debug.Log($"[CgsNet Player] Requesting deal {count}...");
            CmdDeal(stack, count);
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdDeal(GameObject stack, int count)
        {
            Debug.Log($"[CgsNet Player] Dealing {count}!");
            var cardStack = stack.GetComponent<CardStack>();
            var cardIds = new string[count];
            for (var i = 0; i < count && cardStack.Cards.Count > 0; i++)
                cardIds[i] = cardStack.PopCard();
            TargetDeal(cardIds);
        }

        [TargetRpc]
        // ReSharper disable once ParameterTypeCanBeEnumerable.Local
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void TargetDeal(string[] cardIds)
        {
            Debug.Log($"[CgsNet Player] Dealt {cardIds}!");
            CgsNetManager.Instance.playController.AddCardsToHand(
                cardIds.Where(cardId => !string.IsNullOrEmpty(cardId) && !UnityCard.Blank.Id.Equals(cardId))
                    .Select(cardId => CardGameManager.Current.Cards[cardId]));
        }

        #endregion

        #region Cards

        public void MoveCardToServer(CardZone cardZone, CardModel cardModel)
        {
            Transform cardModelTransform = cardModel.transform;
            cardModelTransform.SetParent(cardZone.transform);
            cardModel.position = ((RectTransform) cardModelTransform).anchoredPosition;
            cardModel.rotation = cardModelTransform.rotation;
            CmdSpawnCard(cardModel.Id, cardModel.position, cardModel.rotation, cardModel.isFacedown);
            if (cardModel.IsOnline && cardModel.hasAuthority)
                CmdUnSpawnCard(cardModel.gameObject);
            Destroy(cardModel.gameObject);
        }

        [Command]
        private void CmdSpawnCard(string cardId, Vector3 position, Quaternion rotation, bool isFacedown)
        {
            PlayController controller = CgsNetManager.Instance.playController;
            GameObject newCard = Instantiate(controller.cardModelPrefab, controller.playArea.transform);
            var cardModel = newCard.GetComponent<CardModel>();
            cardModel.Value = CardGameManager.Current.Cards[cardId];
            cardModel.position = position;
            cardModel.rotation = rotation;
            cardModel.isFacedown = isFacedown;
            PlayController.SetPlayActions(cardModel);
            NetworkServer.Spawn(newCard);
            cardModel.RpcHideHighlight();
        }

        [Command]
        private void CmdUnSpawnCard(GameObject toUnSpawn)
        {
            NetworkServer.UnSpawn(toUnSpawn);
            Destroy(toUnSpawn);
        }

        #endregion

        #region Dice

        public void RequestNewDie(int min, int max)
        {
            CmdCreateDie(min, max);
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdCreateDie(int min, int max)
        {
            Die die = CgsNetManager.Instance.playController.CreateDie(min, max);
            NetworkServer.Spawn(die.gameObject);
        }

        #endregion

        #region RestartGame

        public void RequestRestart()
        {
            Debug.Log("[CgsNet Player] Requesting restart!...");
            CmdRestart();
        }

        [Command]
        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void CmdRestart()
        {
            Debug.Log("[CgsNet Player] Game server to restart!...");
            CgsNetManager.Instance.Restart();
        }

        [TargetRpc]
        public void TargetRestart()
        {
            Debug.Log("[CgsNet Player] Game is restarting!...");
            CgsNetManager.Instance.playController.ResetPlayArea();
            CgsNetManager.Instance.playController.hand.Clear();
            CurrentDeck = null;
            StartCoroutine(WaitToRestartGame());
        }

        private IEnumerator WaitToRestartGame()
        {
            if (isServer || CardGameManager.Current.DeckSharePreference == SharePreference.Individual)
            {
                CgsNetManager.Instance.playController.ShowDeckMenu();
                Debug.Log("[CgsNet Player] Game restarted!");
                yield break;
            }

            yield return null;

            Debug.Log("[CgsNet Player] Game restarted!");

            switch (CardGameManager.Current.DeckSharePreference)
            {
                case SharePreference.Individual:
                    CgsNetManager.Instance.playController.ShowDeckMenu();
                    break;
                case SharePreference.Share:
                    RequestSharedDeck();
                    break;
                case SharePreference.Ask:
                default:
                    CardGameManager.Instance.Messenger.Ask(ShareDeckRequest,
                        CgsNetManager.Instance.playController.ShowDeckMenu, RequestSharedDeck);
                    break;
            }
        }

        #endregion
    }
}
