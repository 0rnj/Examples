using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using UnityEngine;
using Steamworks;
using UnityEngine.SceneManagement;

public class DataManager : MonoBehaviour
{

    #region Vars

    public static DataManager Instance;

    public int Level;
    public int Rank;
    public int Experience;
    public int Money;

    /// <summary>
    /// Index stores type of lootbox, value - its count
    /// </summary>
    public int[] Lootboxes;

    /// <summary>
    /// Used to store available inventory client-side.
    /// Destroy on release
    /// </summary>
    public bool[,] AvailableItems;

    [Header("Customization")]
    public int[] CurrentCustomizationProfile;
    [Space]
    public CustomizationItem[] BodyColors;
    [Space]
    public CustomizationItem[] Faces;
    [Space]
    public CustomizationItem[] Hats;
    [Space]
    public CustomizationItem[] Accessories;
    [Space]
    public CustomizationItem[] Emotions;
    [Space]
    public CustomizationItem[] _Lootboxes;
    [Space]
    public UnityEngine.U2D.SpriteAtlas RankAtlas;

    /// <summary>
    /// Items available from the very beginning
    /// </summary>
    private List<int> StarterPack = new List<int>() { 200 };

    /// Callbacks
    public SteamInventoryResult_t InventoryResult, LootboxResult, ItemResult;
    private CallResult<RemoteStorageFileReadAsyncComplete_t> m_FileReadAsync;
    private CallResult<SteamInventoryFullUpdate_t> m_InventoryFullUpdate;
    private Callback<SteamInventoryResultReady_t> m_InventoryResultReady;
    private Callback<SteamInventoryResultReady_t> m_LootboxResultReady;
    private Callback<SteamInventoryResultReady_t> m_ItemResultReady;

    /// Other
    public static Dictionary<int, int> LootboxRecipes = new Dictionary<int, int>();
    private int _lootboxesRequested;
    private SteamItemDetails_t[] _receivedLootboxes;

    #endregion



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Init();
            LoadData();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (SteamManager.Initialized)
        {
            Debug.Log("@Data: Registered handlers");
            m_InventoryResultReady = Callback<SteamInventoryResultReady_t>.Create(OnInventoryResultReady);
            m_InventoryFullUpdate = CallResult<SteamInventoryFullUpdate_t>.Create(OnInventoryFullUpdate);
            m_ItemResultReady = Callback<SteamInventoryResultReady_t>.Create(OnItemResultReady);
            GetInventory();
        }
    }

    string Test(string initial)
    {
        /// Testing "overhead" directories (roots)
        var root = Directory.GetDirectoryRoot(Application.persistentDataPath);
        var dirs = Directory.GetDirectories(root);
        string msg = "Dirs:\n";
        foreach (var dir in dirs)
        {
            msg += dir + "\n";
        }
        Debug.LogWarning(msg);

        using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
        {
            byte[] enc = EncryptStringToBytes_Aes(initial, aes.Key, aes.IV);
            string roundtrip = DecryptStringFromBytes_Aes(enc, aes.Key, aes.IV);
            return roundtrip;
        }
    }

    private void Init()
    {
        Level = Rank = 1;
        CurrentCustomizationProfile = new int[8];
        Lootboxes = new int[5];
        AvailableItems = new bool[4, 32];
    }



    #region ----------------- Save & load -----------------

    private void LoadData()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("LoadData: Steam API not initialized");
            return;
        }

        /// Process downloaded data
        if (File.Exists(Application.persistentDataPath + "/savedGames.gd"))
        {
            print("Loading data");
            var encrypted = File.ReadAllBytes(Application.persistentDataPath + "/savedGames.gd");

            using (AesCryptoServiceProvider myAes = new AesCryptoServiceProvider())
            {
                /// Key, IV
                if (File.Exists(Application.persistentDataPath + "/savedGames.g"))
                {
                    myAes.Key = File.ReadAllBytes(Application.persistentDataPath + "/savedGames.g");
                    myAes.IV = File.ReadAllBytes(Application.persistentDataPath + "/savedGames.d");
                }
                else
                {
                    Debug.LogError("Unable to load saved data");
                    return;
                }

                string json = DecryptStringFromBytes_Aes(encrypted, myAes.Key, myAes.IV);

                var data = JsonUtility.FromJson<SaveData>(json);

                Level = data.Level;
                Experience = data.XP;
                Money = data.Money;
                Rank = data.Rank;
                Lootboxes = data.Lootboxes;
                CurrentCustomizationProfile = data.CustomizationProfile;
                AvailableItems = data.AvailableItems;
            }
        }
        else Debug.LogWarning("Saved data not found");

        /// Apply
        StartCoroutine(Utility.DelayedAction(0.05f, () =>
        {
            /// Get first 4 items
            var preset = new int[4];
            for (int i = 0; i < 4; i++)
            {
                preset[i] = CurrentCustomizationProfile[i];
            }
            MainMenuManager.Instance.PlayerGameobjectAvatar.GetComponent<Customizer>().Preset = preset;
        }));
    }

    private void OnSteamCloudDataLoaded(RemoteStorageFileReadAsyncComplete_t pCallback, bool bIOFailure)
    {
        print("Data loaded, success: " + bIOFailure);

        if (pCallback.m_eResult == EResult.k_EResultOK)
        {
            byte[] data = new byte[pCallback.m_cubRead];
            if (SteamRemoteStorage.FileReadAsyncComplete(pCallback.m_hFileReadAsync, data, pCallback.m_cubRead))
            {
                string m_Message = System.Text.Encoding.UTF8.GetString(data, (int)pCallback.m_nOffset, (int)pCallback.m_cubRead);
                Debug.LogWarning("RemoteStorageFileReadAsyncComplete: Got data from SteamCloud: " + m_Message);
            }
        }
    }

    public void SaveData()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("SaveData: Steam API not initialized");
            return;
        }

        if (C.LOG_DM) Debug.Log("Saving data");
        int[] stats = new int[]
        {
            Level, Experience, Money, Rank
        };
        var data = new SaveData(stats, CurrentCustomizationProfile, Lootboxes, AvailableItems);


        /// Encrypt file and send to SteamCloud
        /// Order for encrypt: json -> aes -> binary;
        /// Reverse order for decrypt

        /// Using JSON
        var json = JsonUtility.ToJson(data);

        /// Using Aes
        using (AesCryptoServiceProvider myAes = new AesCryptoServiceProvider())
        {            
            /// Key, IV
            if (!File.Exists(Application.persistentDataPath + "/savedGames.g") /*|| !File.Exists(Application.persistentDataPath + "/savedGames.d")*/)
            {
                print("Creating keys");
                File.WriteAllBytes(Application.persistentDataPath + "/savedGames.g", myAes.Key);
                File.WriteAllBytes(Application.persistentDataPath + "/savedGames.d", myAes.IV);
            }
            else
            {
                myAes.Key = File.ReadAllBytes(Application.persistentDataPath + "/savedGames.g");
                myAes.IV = File.ReadAllBytes(Application.persistentDataPath + "/savedGames.d");
            }

            var encrypted = EncryptStringToBytes_Aes(json, /*_key*/ myAes.Key, /*_iv*/ myAes.IV);
            File.WriteAllBytes(Application.persistentDataPath + "/savedGames.gd", encrypted);
        }
    }

    static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
    {
        /// Check arguments
        if (plainText == null || plainText.Length <= 0)
            throw new ArgumentNullException("plainText");
        if (Key == null || Key.Length <= 0)
            throw new ArgumentNullException("Key");
        if (IV == null || IV.Length <= 0)
            throw new ArgumentNullException("IV");
        byte[] encrypted;

        /// Create an AesCryptoServiceProvider object
        /// with the specified key and IV.
        using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
        {
            aesAlg.Key = Key;
            aesAlg.IV = IV;

            /// Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            /// Create the streams used for encryption.
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        /// Write all data to the stream.
                        swEncrypt.Write(plainText);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }
        }

        /// Return the encrypted bytes from the memory stream.
        return encrypted;
    }

    static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
    {
        /// Check arguments.
        if (cipherText == null || cipherText.Length <= 0)
            throw new ArgumentNullException("cipherText");
        if (Key == null || Key.Length <= 0)
            throw new ArgumentNullException("Key");
        if (IV == null || IV.Length <= 0)
            throw new ArgumentNullException("IV");

        /// Declare the string used to hold
        /// the decrypted text.
        string plaintext = null;

        /// Create an AesCryptoServiceProvider object
        /// with the specified key and IV.
        using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
        {
            aesAlg.Key = Key;
            aesAlg.IV = IV;

            /// Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            /// Create the streams used for decryption.
            using (MemoryStream msDecrypt = new MemoryStream(cipherText))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {

                        /// Read the decrypted bytes from the decrypting stream
                        /// and place them in a string.
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }

        }

        return plaintext;
    }

    #endregion



    #region ----------------- Customization & rewards -----------------

    /// <summary>
    /// Returns model/sprite object from database.
    /// Null if object not found by type & id
    /// </summary>
    public UnityEngine.Object GetItemObject(CustomizationItemType type, int id)
    {
        CustomizationItem[] itemArray = new CustomizationItem[0];
        switch (type)
        {
            case CustomizationItemType.Color:
                itemArray = BodyColors;
                break;
            case CustomizationItemType.Face:
                itemArray = Faces;
                break;
            case CustomizationItemType.Hat:
                itemArray = Hats;
                break;
            case CustomizationItemType.Accessory:
                itemArray = Accessories;
                break;

            case CustomizationItemType.Emotion:
                itemArray = Emotions;
                break;
        }
        for (int i = 0; i < itemArray.Length; i++)
        {
            if (itemArray[i].ID == id)
                return itemArray[i].ModelPrefab;
        }
        return null;
    }

    /// <summary>
    /// Stores given rewards to be displayed in main menu
    /// </summary>
    public void StoreRewards(bool wonGame, int frags)
    {
        if (C.LOG_DM) Debug.Log("Storing rewards; Won game: " + wonGame + ", frags: " + frags.ToString());

        var reward = new StoredReward();
        if (wonGame)
        {
            reward.Experience = C.WIN_XP + (frags * C.FRAG_XP);
            reward.Money = C.WIN_MONEY + (frags * C.FRAG_MONEY);
        }
        else
        {
            reward.Experience = C.LOSS_XP + (frags * C.FRAG_XP);
            reward.Money = C.LOSS_MONEY + (frags * C.FRAG_MONEY);
        }

        GameManager.Instance.GotRewards = true;
        GameManager.Instance.Reward = reward;

        if (C.LOG_DM) Debug.Log("Stored reward: Money = " + reward.Money.ToString() + ", XP = " + reward.Experience);
    }

    /// <summary>
    /// Applies stored rewards
    /// </summary>
    public void ApplyRewards()
    {
        var gm = GameManager.Instance;

        if (C.LOG_DM) Debug.Log("Applying reward: Money = " + gm.Reward.Money.ToString() + ", XP = " + gm.Reward.Experience);

        /// Applying stored rewards
        Experience += gm.Reward.Experience;
        Money += gm.Reward.Money;
        gm.Reward = new StoredReward();
        gm.GotRewards = false;
        _lootboxesRequested = 0;

        StartCoroutine(Utility.DelayedAction(0.5f, () =>    /// Don't show exactly at moment scene has loaded
        {
            /// Level-up logic
            foreach (var xpData in C.XP_TO_LEVELUP)
            {
                if (Level >= xpData.Key.x && Level <= xpData.Key.y)
                {
                    int c = 0;
                    while (Experience >= xpData.Value && c < 100)
                    {
                        if (C.LOG_DM) Debug.LogWarning("Level up!");

                        /// Xp, level
                        Experience -= xpData.Value;
                        Level++;
                        if (Level > C.MAX_LEVEL) Level -= C.MAX_LEVEL;

                        /// Rank
                        if (Level % C.RANKING_INTERVAL == 0)
                            Rank++;
                        if (Rank > C.MAX_RANK) Rank = C.MAX_RANK;

                        /// Lootbox
                        _lootboxesRequested++;

                        /// Display new stats
                        FindObjectOfType<MenuHUD>().UpdateStats();

                        /// Prevent levelup in case when it takes more (or less) xp for next level (no discounts!)
                        if (Level != xpData.Key.y) break;
                    }
                    if (C.LOG_DM && c >= 100) Debug.LogWarning("@DataManager ApplyRewards: Critical break of while-cycle");
                }
            }

            /// Request lootboxes
            if (_lootboxesRequested > 0)
            {
                if (C.LOG_DM) Debug.LogWarning("Requesting " + _lootboxesRequested + " lootboxes...");
                m_LootboxResultReady = Callback<SteamInventoryResultReady_t>.Create(OnLootboxResultReady);
                for (int i = 0; i < _lootboxesRequested; i++)
                {
                    SteamInventory.TriggerItemDrop(out LootboxResult, new SteamItemDef_t(900));
                }
                StartCoroutine(LootboxAwait());
            }
        }));
    }

    #endregion



    #region ----------------- Inventory -----------------

    public void GetInventory()
    {
        if (C.LOG_DM) Debug.Log("Updating inventory");

        if (SteamInventory.GetAllItems(out InventoryResult))
        {
            if (C.LOG_DM) Debug.Log("Inventory available, InventoryResult: " + InventoryResult.m_SteamInventoryResult);
        }
    }

    private void OnInventoryResultReady(SteamInventoryResultReady_t pCallback)
    {
        Debug.Log("@OnInventoryResultReady called, callback params: " + pCallback.m_handle + ", " + pCallback.m_result);

        InventoryResult = pCallback.m_handle;
        uint size = 0;
        SteamItemDetails_t[] items;
        bool result = SteamInventory.GetResultItems(InventoryResult, null, ref size);

        if (result && size > 0)
        {
            items = new SteamItemDetails_t[size];
            bool ret = SteamInventory.GetResultItems(InventoryResult, items, ref size);
            Debug.LogWarning("@OnInventoryResultReady: Inventory fetch result: " + ret + " with items num: " + items.Length);

            /// Add anew
            UpdateInventory(items, display: false);
        }
        else
        {
            Debug.LogWarning("@OnInventoryResultReady: InventoryResult = " + InventoryResult.m_SteamInventoryResult + ", Size = " + size + ". Could not get result items, problem may be in:\n" +
                "- InventoryResult оказался недействительным, либо дескриптор результата действий с инвентарём оказался не готов.\n" +
                "- Массив не уместился в pOutItemsArray.\n" +
                "- У пользователя нет предметов.");
        }

        /// Dispose result
        SteamInventory.DestroyResult(InventoryResult);
    }

    private void OnLootboxResultReady(SteamInventoryResultReady_t pCallback)
    {
        Debug.Log("@OnLootboxResultReady called, callback params: " + pCallback.m_handle + ", " + pCallback.m_result);

        LootboxResult = pCallback.m_handle;
        uint size = 0;
        SteamItemDetails_t[] items;
        bool result = SteamInventory.GetResultItems(LootboxResult, null, ref size);

        if (result && size > 0)
        {
            items = new SteamItemDetails_t[size];
            result = SteamInventory.GetResultItems(LootboxResult, items, ref size);
            Debug.LogWarning("@OnLootboxResultReady: Get lootbox result: " + result + " with items num: " + items.Length);

            UpdateInventory(items, display: true);

            /// Dispose results and update
            Debug.Log("Disposing resources");
            SteamInventory.DestroyResult(LootboxResult);
            _lootboxesRequested -= items.Length;
            if (_lootboxesRequested <= 0) m_LootboxResultReady.Dispose();
        }
    }

    private void OnItemResultReady(SteamInventoryResultReady_t pCallback)
    {
        Debug.Log("@OnItemResultReady called, callback params: " + pCallback.m_handle + ", " + pCallback.m_result);

        ItemResult = pCallback.m_handle;
        uint size = 0;
        SteamItemDetails_t[] items;
        bool result = SteamInventory.GetResultItems(ItemResult, null, ref size);

        if (result && size > 0)
        {
            items = new SteamItemDetails_t[size];
            result = SteamInventory.GetResultItems(ItemResult, items, ref size);
            Debug.LogWarning("@OnItemResultReady: Get item result: " + result + " with items num: " + items.Length);

            UpdateInventory(items, display: true);
        }
    }


    void UpdateInventory(SteamItemDetails_t[] items, bool display)
    {
        if (C.LOG_DM) Debug.Log("\nAdding new items to inventory");
        List<KeyValuePair<Sprite, string>> itemsToShow = new List<KeyValuePair<Sprite, string>>();

        /// Process fetched items
        for (int i = 0; i < items.Length; i++)
        {
            if (C.LOG_DM)
                Debug.Log("Item #" + (i + 1).ToString() +
                ":\n\tDefinition: " + items[i].m_iDefinition +
                "\n\tItem ID: " + items[i].m_itemId +
                "\n\tQuantity: " + items[i].m_unQuantity +
                "\n\tFlags: " + items[i].m_unFlags);


            /// Hats
            for (int k = 0; k < Hats.Length; k++)
            {
                if (items[i].m_iDefinition.m_SteamItemDef == Hats[k].ID)
                {
                    if (items[i].m_unQuantity == 0)
                    {
                        if (Hats[k].Quantity > 0)
                        {
                            Hats[k].Quantity--;
                            Hats[k].Details.Remove(items[i]);
                        }
                        if (Hats[k].Quantity == 0)
                        {
                            Hats[k].IsAvailable = false;
                        }
                    }
                    else    /// either it will be 1
                    {
                        Hats[k].IsAvailable = true;
                        Hats[k].Quantity++;
                        Hats[k].Details.Add(items[i]);
                        itemsToShow.Add(new KeyValuePair<Sprite, string>(Hats[k].PreviewImage, Hats[k].Name));
                    }
                }
            }

            /// Lootbox
            for (int k = 0; k < _Lootboxes.Length; k++)
            {
                if (items[i].m_iDefinition.m_SteamItemDef == _Lootboxes[k].ID)
                {
                    if (items[i].m_unQuantity == 0)
                    {
                        if (_Lootboxes[k].Quantity > 0)
                        {
                            _Lootboxes[k].Quantity--;
                            _Lootboxes[k].Details.Remove(items[i]);
                        }
                        if (_Lootboxes[k].Quantity == 0)
                        {
                            _Lootboxes[k].IsAvailable = false;
                        }
                    }
                    else
                    {
                        _Lootboxes[k].IsAvailable = true;
                        _Lootboxes[k].Quantity++;
                        _Lootboxes[k].Details.Add(items[i]);
                        itemsToShow.Add(new KeyValuePair<Sprite, string>(_Lootboxes[k].PreviewImage, _Lootboxes[k].Name));
                    }
                }
            }
        }

        if (display)
            MainMenuManager.Instance.ShowNewItems(itemsToShow);
    }


    /// <summary>
    /// Sets all items unavailable (except given by default at the very beginning).
    /// Should be called before Steam inventory is updated
    /// </summary>
    void FlushInventory()
    {
        /// Hats
        for (int i = 0; i < Hats.Length; i++)
        {
            Hats[i].IsAvailable = StarterPack.Contains(Hats[i].ID);
            Hats[i].Details.Clear();
        }

        /// Lootboxes
        for (int i = 0; i < _Lootboxes.Length; i++)
        {
            _Lootboxes[i].IsAvailable = StarterPack.Contains(_Lootboxes[i].ID);
            _Lootboxes[i].Details.Clear();
        }
    }

    #endregion



    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        print("Scene loaded: " + scene.name);
        Cursor.visible = true;
    }

    private void OnApplicationQuit()
    {
        SaveData();
    }
}