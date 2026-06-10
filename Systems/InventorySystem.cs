using UnityEngine;
using System.Collections.Generic;
using KKN.Game.Data;
using KKN.Game.Inventory;

namespace KKN.Game.Systems
{
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

        // Quick Items
        private Dictionary<ItemData, int> items = new Dictionary<ItemData, int>();
        private HashSet<string> legacyItems = new HashSet<string>();

        // Documents
        private List<DocumentData> documents = new List<DocumentData>();

        public event System.Action<ItemData> OnItemAdded;
        public event System.Action<ItemData> OnItemRemoved;
        public event System.Action OnInventoryChanged;
        public event System.Action OnDocumentChanged;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // =====================
        // QUICK ITEM
        // =====================

        public void AddItem(ItemData item)
        {
            if (item == null) return;

            if (!items.ContainsKey(item))
                items[item] = 0;

            items[item]++;

            OnItemAdded?.Invoke(item);
            OnInventoryChanged?.Invoke();
        }

        public void AddItem(string itemID)
        {
            legacyItems.Add(itemID);
            OnInventoryChanged?.Invoke();
        }

        public bool HasItem(string itemID)
        {
            return legacyItems.Contains(itemID);
        }

        public bool HasItem(ItemData item)
        {
            return items.ContainsKey(item);
        }

        public bool ConsumeItem(ItemData item)
        {
            if (!HasItem(item)) return false;

            items[item]--;

            if (items[item] <= 0)
                items.Remove(item);

            OnItemRemoved?.Invoke(item);
            OnInventoryChanged?.Invoke();

            return true;
        }

        // =====================
        // DOCUMENT
        // =====================

        public void AddDocument(DocumentData doc)
        {
            if (doc == null) return;

            if (!documents.Contains(doc))
            {
                documents.Add(doc);
                OnDocumentChanged?.Invoke();
            }
        }

        public bool HasDocument(DocumentData doc)
        {
            return documents.Contains(doc);
        }

        public List<DocumentData> GetDocuments()
        {
            return documents;
        }

        /// <summary>Mengembalikan semua ItemData yang ada di inventory beserta jumlahnya.</summary>
        public List<ItemData> GetItems()
        {
            return new List<ItemData>(items.Keys);
        }

        /// <summary>Mengembalikan jumlah stack item tertentu (0 jika tidak ada).</summary>
        public int GetItemCount(ItemData item)
        {
            return items.TryGetValue(item, out int count) ? count : 0;
        }

        /// <summary>Alias ConsumeItem — dipakai TabInventoryUI tombol GUNAKAN.</summary>
        public bool UseItem(ItemData item) => ConsumeItem(item);

        /// <summary>Alias ConsumeItem — dipakai TabInventoryUI tombol BUANG.</summary>
        public bool RemoveItem(ItemData item) => ConsumeItem(item);

        // =====================

        public void ClearInventory()
        {
            items.Clear();
            legacyItems.Clear();
            documents.Clear();

            OnInventoryChanged?.Invoke();
            OnDocumentChanged?.Invoke();
        }
    }
}