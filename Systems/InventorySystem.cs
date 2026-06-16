using UnityEngine;
using System.Collections.Generic;
using System;
using KKN.Game.Data;
using KKN.Game.Inventory;

namespace KKN.Game.Systems
{
    /// <summary>
    /// Central inventory — menyimpan item (key/material/other) dan dokumen.
    /// Semua perubahan memancarkan event agar UI bisa reaktif.
    /// </summary>
    public class InventorySystem : MonoBehaviour
    {
        public static InventorySystem Instance { get; private set; }

        // ── Data registry ─────────────────────────────────
        [Header("Item Registry")]
        [Tooltip("Daftarkan semua ItemData ScriptableObject di sini")]
        [SerializeField] private List<ItemData> itemRegistry = new();

        // ── Runtime storage ───────────────────────────────
        // key: itemID, value: jumlah
        private Dictionary<string, int>      items     = new();
        private List<DocumentData>            documents = new();

        // ── Events (UI subscribe ke sini) ─────────────────
        public event Action<string, int>      OnItemChanged;   // itemID, newCount
        public event Action<DocumentData>     OnDocumentAdded;
        public event Action                   OnInventoryChanged; // general refresh

        // ── Lifecycle ─────────────────────────────────────
        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ══════════════════════════════════════════════════
        //  ITEMS
        // ══════════════════════════════════════════════════
        public event Action<ItemData, int> OnMaterialAdded;
        public void AddItem(string itemID, int amount = 1)
        {
            Debug.Log($"ADD ITEM CALLED : {itemID}");

            if (string.IsNullOrEmpty(itemID))
                return;

            if (items.ContainsKey(itemID))
                items[itemID] += amount;
            else
                items[itemID] = amount;

            OnItemChanged?.Invoke(itemID, items[itemID]);

            var data = GetItemData(itemID);

            Debug.Log($"DATA = {(data != null ? data.displayName : "NULL")}");

            if (data != null)
            {
                Debug.Log($"TYPE = {data.itemType}");

                if (string.Equals(
                    data.itemType,
                    "Material",
                    StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log("MATERIAL EVENT FIRED");

                    OnMaterialAdded?.Invoke(
                        data,
                        items[itemID]);
                }
            }

            OnInventoryChanged?.Invoke();

            Debug.Log(
                $"[InventorySystem] +{amount} '{itemID}' → total {items[itemID]}");
        }

        public bool HasItem(string itemID, int amount = 1)
        {
            return items.TryGetValue(itemID, out int count) && count >= amount;
        }

        public bool RemoveItem(string itemID, int amount = 1)
        {
            if (!HasItem(itemID, amount)) return false;

            items[itemID] -= amount;
            if (items[itemID] <= 0) items.Remove(itemID);

            OnItemChanged?.Invoke(itemID, items.ContainsKey(itemID) ? items[itemID] : 0);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public int GetItemCount(string itemID) =>
            items.TryGetValue(itemID, out int c) ? c : 0;

        public Dictionary<string, int> GetAllItems() => new(items);

        // ── ItemData lookup ───────────────────────────────
        public ItemData GetItemData(string itemID)
        {
            return itemRegistry.Find(d => d != null && d.itemID == itemID);
        }

        // ══════════════════════════════════════════════════
        //  DOCUMENTS
        // ══════════════════════════════════════════════════

        public void AddDocument(DocumentData doc)
        {
            if (doc == null) return;
            if (documents.Contains(doc)) return;

            doc.isPicked = true;
            documents.Add(doc);

            OnDocumentAdded?.Invoke(doc);
            OnInventoryChanged?.Invoke();

            Debug.Log($"[InventorySystem] Dokumen ditambahkan: '{doc.title}'");
        }

        public bool HasDocument(string documentID) =>
            documents.Exists(d => d.documentID == documentID);

        public List<DocumentData> GetAllDocuments() => new(documents);

        // ══════════════════════════════════════════════════
        //  UTILITY
        // ══════════════════════════════════════════════════

        /// <summary>Ambil semua item dengan itemType tertentu (Key/Material/Other).</summary>
        public List<(ItemData data, int count)> GetItemsByType(string itemType)
        {
            Debug.Log($"ITEM DICTIONARY COUNT = {items.Count}");

            var result = new List<(ItemData, int)>();
            foreach (var kv in items)
            {
                Debug.Log($"ITEM = {kv.Key} | COUNT = {kv.Value}");

                var data = GetItemData(kv.Key);

                Debug.Log(
                    $"ID={kv.Key} " +
                    $"DATA={(data != null ? data.displayName : "NULL")} " +
                    $"TYPE={(data != null ? data.itemType : "NULL")}"
                );

                if (data != null && string.Equals(data.itemType, itemType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    result.Add((data, kv.Value));
                }
            }
            return result;
        }
    }
}
