﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using UnityEngine.UI;

public delegate void LoadJTokenDelegate(JToken jToken,string defaultValue);

[JsonObject(MemberSerialization.OptIn)]
public class CardGame
{
    public const string AllCardsFileName = "AllCards.json";
    public const string AllSetsFileName = "AllSets.json";
    public const string BackgroundImageFileName = "Background";
    public const string CardBackImageFileName = "CardBack";
    public const string DefaultCardImageURLFormat = "{0}/{1}.{2}";
    public const int DefaultDeckMaxSize = 75;
    public const DeckFileType DefaultDeckFileType = DeckFileType.Txt;
    public const int DefaultHandStartSize = 5;
    public const string DefaultHsdPropertyId = "dbfId";
    public const string DefaultImageFileType = "png";
    public const string SetCardsIdentifier = "cards";

    public string FilePathBase {
        get { return CardGameManager.GamesFilePathBase + "/" + Name; }
    }

    public string ConfigFilePath {
        get { return FilePathBase + "/" + Name + ".json"; }
    }

    public string DecksFilePath {
        get  { return FilePathBase + "/decks"; }
    }

    [JsonProperty]
    public string Name { get; set; }

    [JsonProperty]
    public string AllCardsURL { get; set; }

    [JsonProperty]
    public bool AllCardsZipped { get; set; }

    [JsonProperty]
    public string AllSetsURL { get; set; }

    [JsonProperty]
    public bool AllSetsZipped { get; set; }

    [JsonProperty]
    public bool AutoUpdate { get; set; }

    [JsonProperty]
    public string AutoUpdateURL { get; set; }

    [JsonProperty]
    public string BackgroundImageFileType { get; set; }

    [JsonProperty]
    public string BackgroundImageURL { get; set; }

    [JsonProperty]
    public string CardBackImageFileType { get; set; }

    [JsonProperty]
    public string CardBackImageURL { get; set; }

    [JsonProperty]
    public string CardIdIdentifier { get; set; }

    [JsonProperty]
    public string CardImageFileType { get; set; }

    [JsonProperty]
    public string CardImageURLBase { get; set; }

    [JsonProperty]
    public string CardImageURLFormat { get; set; }

    [JsonProperty]
    public string CardImageURLProperty { get; set; }

    [JsonProperty]
    public string CardNameIdentifier { get; set; }

    [JsonProperty]
    public string CardSetIdentifier { get; set; }

    [JsonProperty]
    public string CardPrimaryProperty { get; set; }

    [JsonProperty]
    public List<PropertyDef> CardProperties { get; set; }

    [JsonProperty]
    public DeckFileType DeckFileType { get; set; }

    [JsonProperty]
    public int DeckMaxSize { get; set; }

    [JsonProperty]
    public List<EnumDef> Enums { get; set; }

    [JsonProperty]
    public List<ExtraDef> Extras { get; set; }

    [JsonProperty]
    public int HandStartSize { get; set; }

    [JsonProperty]
    public string HsdPropertyId { get; set; }

    [JsonProperty]
    public string SetCodeIdentifier { get; set; }

    [JsonProperty]
    public string SetNameIdentifier { get; set; }

    public bool IsLoading { get; private set; }

    public bool IsLoaded { get; private set; }

    public string Error { get; private set; }

    private Sprite _backgroundImageSprite;
    private Sprite _cardBackImageSprite;
    private Dictionary<string, Card> _cards;
    private Dictionary<string, Set> _sets;

    public CardGame(string name = Set.DefaultCode, string url = "")
    {
        Name = name ?? Set.DefaultCode;
        AutoUpdateURL = url ?? string.Empty;

        AllCardsURL = string.Empty;
        AllSetsURL = string.Empty;

        BackgroundImageFileType = DefaultImageFileType;
        BackgroundImageURL = string.Empty;
        CardBackImageFileType = DefaultImageFileType;
        CardBackImageURL = string.Empty;
        CardImageURLBase = string.Empty;
        CardImageURLFormat = DefaultCardImageURLFormat;
        CardImageURLProperty = string.Empty;
        CardImageFileType = DefaultImageFileType;
        CardIdIdentifier = "id";
        CardNameIdentifier = "name";
        CardSetIdentifier = "set";
        CardProperties = new List<PropertyDef>();
        DeckFileType = DefaultDeckFileType;
        DeckMaxSize = DefaultDeckMaxSize;
        Enums = new List<EnumDef>();
        Extras = new List<ExtraDef>();
        HandStartSize = DefaultHandStartSize;
        HsdPropertyId = DefaultHsdPropertyId;
        SetCodeIdentifier = "code";
        SetNameIdentifier = "name";
    }

    public IEnumerator Load()
    {
        if (IsLoading)
            yield break;
        IsLoading = true;

        string initialDirectory = FilePathBase;
        if (!string.IsNullOrEmpty(AutoUpdateURL) && (AutoUpdate || !File.Exists(ConfigFilePath)))
            yield return UnityExtensionMethods.SaveURLToFile(AutoUpdateURL, ConfigFilePath);
        try {
            JsonConvert.PopulateObject(File.ReadAllText(ConfigFilePath), this);
        } catch (Exception e) {
            Debug.LogError("Failed to load card game! Error: " + e.Message + e.StackTrace);
            Error = e.Message;
            IsLoading = false;
            yield break;
        }
        if (AutoUpdate || !initialDirectory.Equals(FilePathBase)) {
            yield return UnityExtensionMethods.SaveURLToFile(AutoUpdateURL, ConfigFilePath);
            if (!initialDirectory.Equals(FilePathBase))
                Directory.Delete(initialDirectory, true);
        }

        Sprite backgroundSprite = null;
        yield return UnityExtensionMethods.RunOutputCoroutine<Sprite>(UnityExtensionMethods.CreateAndOutputSpriteFromImageFile(FilePathBase + "/" + BackgroundImageFileName + "." + BackgroundImageFileType, BackgroundImageURL), output => backgroundSprite = output);
        if (backgroundSprite != null)
            BackgroundImageSprite = backgroundSprite;

        Sprite cardBackSprite = null;
        yield return UnityExtensionMethods.RunOutputCoroutine<Sprite>(UnityExtensionMethods.CreateAndOutputSpriteFromImageFile(FilePathBase + "/" + CardBackImageFileName + "." + CardBackImageFileType, CardBackImageURL), output => cardBackSprite = output);
        if (cardBackSprite != null)
            CardBackImageSprite = cardBackSprite;

        string cardsFile = FilePathBase + "/" + AllCardsFileName;
        if (!string.IsNullOrEmpty(AllCardsURL) && (AutoUpdate || !File.Exists(cardsFile))) {
            yield return UnityExtensionMethods.SaveURLToFile(AllCardsURL, AllCardsZipped ? cardsFile + ".zip" : cardsFile);
            if (AllCardsZipped)
                UnityExtensionMethods.ExtractZip(cardsFile + ".zip", FilePathBase);
        }
        string setsFile = FilePathBase + "/" + AllSetsFileName;
        if (!string.IsNullOrEmpty(AllSetsURL) && (AutoUpdate || !File.Exists(setsFile))) {
            yield return UnityExtensionMethods.SaveURLToFile(AllSetsURL, AllSetsZipped ? setsFile + ".zip" : setsFile);
            if (AllSetsZipped)
                UnityExtensionMethods.ExtractZip(setsFile + ".zip", FilePathBase);
        }
        try {
            LoadJSONFromFile(cardsFile, LoadCardFromJToken);
            LoadJSONFromFile(setsFile, LoadSetFromJToken);
        } catch (Exception e) {
            Debug.LogError("Failed to load card game data! Error: " + e.Message + e.StackTrace);
            Error = e.Message;
            IsLoading = false;
            yield break;
        }

        IsLoading = false;
        IsLoaded = true;
    }

    public void LoadJSONFromFile(string file, LoadJTokenDelegate load)
    {
        if (!File.Exists(file))
            return;
        
        JToken root = JToken.Parse(File.ReadAllText(file));
        IJEnumerable<JToken> jTokenEnumeration = root as JArray;
        if (jTokenEnumeration == null)
            jTokenEnumeration = (root as JObject).PropertyValues();
        foreach (JToken jToken in jTokenEnumeration)
            load(jToken, Set.DefaultCode);
    }

    public void LoadCardFromJToken(JToken cardJToken, string defaultSetCode)
    {
        if (cardJToken == null)
            return;

        string cardId = cardJToken.Value<string>(CardIdIdentifier) ?? string.Empty;
        string cardName = cardJToken.Value<string>(CardNameIdentifier) ?? string.Empty;
        string cardSet = cardJToken.Value<string>(CardSetIdentifier) ?? defaultSetCode;
        Dictionary<string, PropertyDefValuePair> cardProperties = new Dictionary<string, PropertyDefValuePair>();
        foreach (PropertyDef property in CardProperties) {
            cardProperties [property.Name] = new PropertyDefValuePair() {
                Def = property,
                Value = cardJToken.Value<string>(property.Name) ?? string.Empty
            };
        }
        if (!string.IsNullOrEmpty(cardId)) {
            Card newCard = new Card(cardId, cardName, cardSet, cardProperties);
            Cards [newCard.Id] = newCard;
            if (!Sets.ContainsKey(cardSet))
                Sets [cardSet] = new Set(cardSet);
        }
    }

    public void LoadSetFromJToken(JToken setJToken, string defaultSetCode)
    {
        if (setJToken == null)
            return;
        
        string setCode = setJToken.Value<string>(SetCodeIdentifier) ?? defaultSetCode;
        string setName = setJToken.Value<string>(SetNameIdentifier) ?? defaultSetCode;
        if (!string.IsNullOrEmpty(setCode) && !string.IsNullOrEmpty(setName))
            Sets [setCode] = new Set(setCode, setName);
        JArray cards = setJToken.Value<JArray>(SetCardsIdentifier);
        if (cards != null)
            foreach (JToken jToken in cards)
                LoadCardFromJToken(jToken, setCode);
    }

    public IEnumerable<Card> FilterCards(string id, string name, string setCode, Dictionary<string, string> stringProperties, Dictionary<string, int> intMinProperties, Dictionary<string, int> intMaxProperties, Dictionary<string, int> enumProperties)
    {
        if (id == null)
            id = string.Empty;
        if (name == null)
            name = string.Empty;
        if (setCode == null)
            setCode = string.Empty;
        if (stringProperties == null)
            stringProperties = new Dictionary<string, string>();
        if (intMinProperties == null)
            intMinProperties = new Dictionary<string, int>();
        if (intMaxProperties == null)
            intMaxProperties = new Dictionary<string, int>();
        if (enumProperties == null)
            enumProperties = new Dictionary<string, int>();

        foreach (Card card in Cards.Values) {
            if (card.Id.ToLower().Contains(id.ToLower())
                && card.Name.ToLower().Contains(name.ToLower())
                && card.SetCode.ToLower().Contains(setCode.ToLower())) {
                bool propsMatch = true;
                foreach (KeyValuePair<string, string> entry in stringProperties)
                    if (!card.GetPropertyValueString(entry.Key).ToLower().Contains(entry.Value.ToLower()))
                        propsMatch = false;
                int intValue;
                foreach (KeyValuePair<string, int> entry in intMinProperties)
                    if (int.TryParse(card.GetPropertyValueString(entry.Key), out intValue) && intValue < entry.Value)
                        propsMatch = false;
                foreach (KeyValuePair<string, int> entry in intMaxProperties)
                    if (int.TryParse(card.GetPropertyValueString(entry.Key), out intValue) && intValue > entry.Value)
                        propsMatch = false;
                foreach (KeyValuePair<string, int> entry in enumProperties) {
                    string stringValue = card.GetPropertyValueString(entry.Key);
                    if (stringValue.StartsWith("0x")) {
                        if (EnumDef.TryParseInt(stringValue, out intValue) && (intValue & entry.Value) == 0)
                            propsMatch = false;
                    } else if (EnumDef.IsEnumProperty(entry.Key)) {
                        string stringValue2;
                        EnumDef enumDef = Enums.Where(def => def.Property.Equals(entry.Key)).First();
                        if (enumDef.Lookup.TryGetValue(entry.Value, out stringValue2) && !stringValue.Equals(stringValue2))
                            propsMatch = false;
                        // TODO: ALLOW FOR MULTIPLE ENUM SELECTION (OR VS AND)
                    } else
                        propsMatch = false;
                }
                if (propsMatch)
                    yield return card;
            }
        }
    }

    public void PutCardImage(CardModel cardModel)
    {
        Card card = cardModel.Value;
        card.ModelsUsingImage.Add(cardModel);

        if (card.ImageSprite != null)
            cardModel.GetComponent<Image>().sprite = card.ImageSprite;
        else if (!card.IsLoadingImage)
            CardGameManager.Instance.StartCoroutine(GetAndSetImageSprite(card));
    }

    public IEnumerator GetAndSetImageSprite(Card card)
    {
        if (card.IsLoadingImage)
            yield break;

        card.IsLoadingImage = true;
        Sprite newSprite = null;
        yield return UnityExtensionMethods.RunOutputCoroutine<Sprite>(UnityExtensionMethods.CreateAndOutputSpriteFromImageFile(card.ImageFilePath, card.ImageWebURL), output => newSprite = output);
        if (newSprite != null)
            card.ImageSprite = newSprite;
        else
            newSprite = CardGameManager.Current.CardBackImageSprite;

        foreach (CardModel cardModel in card.ModelsUsingImage)
            cardModel.GetComponent<Image>().sprite = newSprite;
        card.IsLoadingImage = false;
    }

    public void RemoveCardImage(CardModel cardModel)
    {
        Card card = cardModel.Value;
        card.ModelsUsingImage.Remove(cardModel);
        if (card.ModelsUsingImage.Count < 1)
            card.ImageSprite = null;
    }

    public Sprite BackgroundImageSprite {
        get {
            if (_backgroundImageSprite == null)
                _backgroundImageSprite = Resources.Load<Sprite>(BackgroundImageFileName);
            return _backgroundImageSprite;
        }
        private set {
            _backgroundImageSprite = value;
        }
    }

    public Sprite CardBackImageSprite {
        get {
            if (_cardBackImageSprite == null)
                _cardBackImageSprite = Resources.Load<Sprite>(CardBackImageFileName);
            return _cardBackImageSprite;
        }
        private set {
            _cardBackImageSprite = value;
        }
    }

    public Dictionary<string, Card> Cards {
        get {
            if (_cards == null)
                _cards = new Dictionary<string, Card>();
            return _cards;
        }
    }

    public Dictionary<string, Set> Sets {
        get {
            if (_sets == null)
                _sets = new Dictionary<string, Set>();
            return _sets;
        }
    }
}
