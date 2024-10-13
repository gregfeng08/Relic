using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem inst;
    public static Dictionary<string, List<Item>> inventoryMap = new Dictionary<string, List<Item>>(); //UUID to ItemList

    public class Item
    {
        string UUID;
        string name;
        string flavorText;

        void Awake()
        {
            Guid myuuid = Guid.NewGuid();
            UUID = myuuid.ToString();
        }

        public override string ToString()
        {
            string jsonOutput = $"\"UUID\":{UUID}";
            return jsonOutput;
        }
    }

    class Weapons : Item
    {
        
    }

    class Staff : Weapons
    {

    }

    void Awake() //Singleton pattern
    {
        if(inst != null && inst != this)
        {
            Destroy(gameObject);
        } else
        {
            inst = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
