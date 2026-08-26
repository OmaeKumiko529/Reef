using System.Collections.Generic;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    static BlockManager _instance;

    public static BlockManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<BlockManager>();
                if (_instance == null)
                {
                    var go = new GameObject("BlockManager");
                    _instance = go.AddComponent<BlockManager>();
                }
            }
            return _instance;
        }
    }

    ProvinceDatabase database;
    Dictionary<string, BlockState> states = new Dictionary<string, BlockState>();
    BlockDetailPanel panel;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public void Setup(ProvinceDatabase db, Font font)
    {
        if (db == null) return;
        database = db;

        foreach (var p in db.provinces)
        {
            if (p == null || string.IsNullOrEmpty(p.id)) continue;
            states[p.id] = BlockState.CreatePlaceholder(p);
        }

        EnsurePanel(font);
    }

    void EnsurePanel(Font font)
    {
        if (panel != null) return;
        panel = GetComponent<BlockDetailPanel>();
        if (panel == null) panel = gameObject.AddComponent<BlockDetailPanel>();
        panel.font = font;
        panel.EnsureUI();
    }

    public BlockState GetState(string id)
    {
        states.TryGetValue(id, out var s);
        return s;
    }

    public ProvinceData GetProvince(string id)
    {
        return database != null ? database.GetById(id) : null;
    }

    public void OpenBlock(string id)
    {
        if (panel == null || database == null) return;
        panel.Show(database.GetById(id), GetState(id));
    }

    public void CloseBlock()
    {
        if (panel != null) panel.Hide();
    }
}
