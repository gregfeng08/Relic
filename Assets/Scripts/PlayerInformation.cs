using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInformation : MonoBehaviour
{
    [SerializeField] static string UUID;
    [SerializeField] string playerName;
    [SerializeField] string playerClass;
    [SerializeField] string playerLevel;
    //List<InventorySystem.Item> Inventory = InventorySystem.inventoryMap[UUID];
    Dictionary<string, int> stats;

    void Awake()
    {
        stats = ClassManager.inst.getStats(playerClass);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
