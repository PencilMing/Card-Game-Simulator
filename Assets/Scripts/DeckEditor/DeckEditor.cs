﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public delegate string OnDeckNameChangeDelegate(string newName);

public class DeckEditor : MonoBehaviour, ICardDropHandler
{
    public const string NewDeckPrompt = "Clear the editor and start a new Untitled deck?";
    public const string SaveChangesPrompt = "You have unsaved changes. Would you like to save?";
    public const int CardStackSize = 8;

    public int CardStackCount {
        get { return Mathf.CeilToInt((float)CardGameManager.Current.DeckMaxSize / CardStackSize); }
    }

    public Deck CurrentDeck {
        get { 
            Deck deck = new Deck(SavedDeck != null ? SavedDeck.Name : Deck.DefaultName, CardGameManager.Current.DeckFileType);
            foreach (CardStack stack in CardStacks)
                foreach (CardModel card in stack.GetComponentsInChildren<CardModel>())
                    deck.Cards.Add(card.Value);
            return deck;
        }
    }

    public Deck SavedDeck { get; private set; }

    public bool HasChanged {
        get {
            Deck currentDeck = CurrentDeck;
            if (currentDeck.Cards.Count < 1)
                return false;
            return !currentDeck.Equals(SavedDeck);
        }
    }

    public GameObject cardModelPrefab;
    public GameObject cardStackPrefab;
    public GameObject deckLoadMenuPrefab;
    public GameObject deckSaveMenuPrefab;
    public RectTransform layoutArea;
    public RectTransform layoutContent;
    public Scrollbar scrollBar;
    public Text nameText;
    public Text countText;

    private List<CardStack> _cardStacks;
    private int _currentCardStackIndex;
    private DeckLoadMenu _deckLoader;
    private DeckSaveMenu _deckSaver;

    void OnEnable()
    {
        CardGameManager.Instance.OnSelectActions.Add(ResetCardStacks);
    }

    void Start()
    {
        layoutArea.gameObject.GetOrAddComponent<CardDropZone>().dropHandler = this;
    }

    public void ResetCardStacks()
    {
        Clear();
        layoutContent.DestroyAllChildren();
        CardStacks.Clear();
        for (int i = 0; i < CardStackCount; i++) {
            CardStack newCardStack = Instantiate(cardStackPrefab, layoutContent).GetOrAddComponent<CardStack>();
            newCardStack.type = CardStackType.Vertical;
            newCardStack.scrollRectContainer = layoutArea.gameObject.GetOrAddComponent<ScrollRect>();
            newCardStack.OnAddCardActions.Add(OnAddCardModel);
            newCardStack.OnRemoveCardActions.Add(OnRemoveCardModel);
            CardStacks.Add(newCardStack);
        }
        layoutContent.sizeDelta = new Vector2(cardStackPrefab.GetComponent<RectTransform>().rect.width * CardStacks.Count, layoutContent.sizeDelta.y);
    }

    public void OnDrop(CardModel cardModel)
    {
        AddCardModel(cardModel);
    }

    public void AddCardModel(CardModel cardModel)
    {
        if (cardModel == null || CardStacks.Count < 1)
            return;
        
        EventSystem.current.SetSelectedGameObject(null, cardModel.CurrentPointerEventData);

        AddCard(cardModel.Value);
    }

    public void AddCard(Card card)
    {
        if (card == null || CardStacks.Count < 1)
            return;
        
        int maxCopiesInStack = CardStackSize;
        CardModel newCardModel = null;
        while (newCardModel == null) {
            if (CardStacks [CurrentCardStackIndex].transform.childCount < maxCopiesInStack) {
                newCardModel = Instantiate(cardModelPrefab, CardStacks [CurrentCardStackIndex].transform).GetOrAddComponent<CardModel>();
                newCardModel.Value = card;
            } else {
                CurrentCardStackIndex++;
                if (CurrentCardStackIndex == 0)
                    maxCopiesInStack++;
            }
        }

        float newSpot = cardStackPrefab.GetComponent<RectTransform>().rect.width * ((float)CurrentCardStackIndex + ((CurrentCardStackIndex < CardStacks.Count / 2f) ? 0f : 1f)) / layoutContent.sizeDelta.x;
        scrollBar.value = Mathf.Clamp01(newSpot);

        OnAddCardModel(CardStacks [CurrentCardStackIndex], newCardModel);
    }

    public void OnAddCardModel(CardStack cardStack, CardModel cardModel)
    {
        if (cardStack == null || cardModel == null)
            return;

        CurrentCardStackIndex = CardStacks.IndexOf(cardStack);
        cardModel.SecondaryDragAction = cardModel.UpdateParentCardStackScrollRect;
        cardModel.DoubleClickAction = DestroyCardModel;

        UpdateDeckStats();
    }

    public void OnRemoveCardModel(CardStack cardStack, CardModel cardModel)
    {
        UpdateDeckStats();
    }

    public void DestroyCardModel(CardModel cardModel)
    {
        if (cardModel == null)
            return;

        cardModel.transform.SetParent(null);
        GameObject.Destroy(cardModel.gameObject);
        CardInfoViewer.Instance.IsVisible = false;

        UpdateDeckStats();
    }

    public void Sort()
    {
        Deck sortedDeck = CurrentDeck;
        sortedDeck.Sort();
        LoadDeck(sortedDeck);
    }

    public void PromptForClear()
    {
        CardGameManager.Instance.Messenger.Prompt(NewDeckPrompt, Clear);
    }

    public void Clear()
    {
        foreach (CardStack stack in CardStacks)
            stack.transform.DestroyAllChildren();
        CurrentCardStackIndex = 0;

        CardInfoViewer.Instance.IsVisible = false;
        SavedDeck = null;
        UpdateDeckStats();
    }

    public string UpdateDeckName(string newName)
    {
        if (newName == null)
            newName = string.Empty;
        newName = UnityExtensionMethods.GetSafeFileName(newName);
        nameText.text = newName + (HasChanged ? "*" : "");
        return newName;
    }

    public void UpdateDeckStats()
    {
        string deckName = Deck.DefaultName;
        if (SavedDeck != null)
            deckName = SavedDeck.Name;
        nameText.text = deckName + (HasChanged ? "*" : "");
        countText.text = CurrentDeck.Cards.Count.ToString();
    }

    public void ShowDeckLoadMenu()
    {
        Deck currentDeck = CurrentDeck;
        string orignalText = currentDeck.Cards.Count > 0 ? currentDeck.ToString() : null;
        DeckLoader.Show(CurrentDeck.Name, UpdateDeckName, LoadDeck, orignalText);
    }

    public void LoadDeck(Deck newDeck)
    {
        if (newDeck == null)
            return;

        Clear();
        foreach (Card card in newDeck.Cards)
            AddCard(card);
        SavedDeck = newDeck;
        UpdateDeckStats();
    }

    public void ShowDeckSaveMenu()
    {
        Deck deckToSave = CurrentDeck;
        bool overwrite = SavedDeck != null && deckToSave.Name.Equals(SavedDeck.Name);
        DeckSaver.Show(deckToSave, UpdateDeckName, OnSaveDeck, overwrite);
    }

    public void OnSaveDeck(Deck savedDeck)
    {
        SavedDeck = savedDeck;
        UpdateDeckStats();
    }

    public void CheckBackToMainMenu()
    {
        if (HasChanged) {
            CardGameManager.Instance.Messenger.Ask(SaveChangesPrompt, BackToMainMenu, ShowDeckSaveMenu);
            return;
        }

        BackToMainMenu();
    }

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(MainMenu.MainMenuSceneIndex);
    }

    public List<CardStack> CardStacks {
        get {
            if (_cardStacks == null)
                _cardStacks = new List<CardStack>();
            return _cardStacks;
        }
    }

    public int CurrentCardStackIndex {
        get {
            if (_currentCardStackIndex < 0 || _currentCardStackIndex >= CardStacks.Count)
                _currentCardStackIndex = 0;
            return _currentCardStackIndex;
        }
        set {
            _currentCardStackIndex = value;
        }
    }

    public DeckLoadMenu DeckLoader {
        get {
            if (_deckLoader == null)
                _deckLoader = Instantiate(deckLoadMenuPrefab).GetOrAddComponent<DeckLoadMenu>();
            return _deckLoader;
        }
    }

    public DeckSaveMenu DeckSaver {
        get {
            if (_deckSaver == null)
                _deckSaver = Instantiate(deckSaveMenuPrefab).GetOrAddComponent<DeckSaveMenu>();
            return _deckSaver;
        }
    }

}
