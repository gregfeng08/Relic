using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClassManager : MonoBehaviour
{
    public static ClassManager inst;

    void Awake() //Singleton pattern
    {
        if (inst != null && inst != this)
        {
            Destroy(gameObject);
        }
        else
        {
            inst = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public class PlayerClass
    {
        public Dictionary<string, int> baseStats;

        public PlayerClass()
        {
            baseStats = new Dictionary<string, int>();
        }

    }

    private Dictionary<string, PlayerClass> stringToClass = new Dictionary<string, PlayerClass>()
    {
        { "Mage", new Mage() },
        { "Archer", new Archer() },
        { "Warrior", new Warrior() }
    };

    class Mage : PlayerClass
    {
        public Mage()
        {
            baseStats["str"] = 10; //Melee damage
            baseStats["int"] = 10; //Ability Effectiveness Scaler
            baseStats["cons"] = 10; //Debuff Resist
            baseStats["agi"] = 10; //Move Speed
            baseStats["dex"] = 10; //Attack Accuracy
            baseStats["end"] = 10; //Stamina (Food/Water)
            baseStats["luck"] = 10; //Loot Drop, crits
        }
    }

    class Archer : PlayerClass
    {
        public Archer()
        {
            baseStats["str"] = 10; //Melee damage
            baseStats["int"] = 10; //Ability Effectiveness Scaler
            baseStats["cons"] = 10; //Debuff Resist
            baseStats["agi"] = 10; //Move Speed
            baseStats["dex"] = 10; //Attack Accuracy
            baseStats["end"] = 10; //Stamina (Food/Water)
            baseStats["luck"] = 10; //Loot Drop, crits
        }
    }

    class Warrior : PlayerClass
    {
        public Warrior()
        {
            baseStats["str"] = 10; //Melee damage
            baseStats["int"] = 10; //Ability Effectiveness Scaler
            baseStats["cons"] = 10; //Debuff Resist
            baseStats["agi"] = 10; //Move Speed
            baseStats["dex"] = 10; //Attack Accuracy
            baseStats["end"] = 10; //Stamina (Food/Water)
            baseStats["luck"] = 10; //Loot Drop, crits
        }
    }

    public Dictionary<string, int> getStats(string className)
    {
        if (stringToClass.ContainsKey(className))
        {
            return stringToClass[className].baseStats;
        } else
        {
            Debug.LogWarning("No Associated Class Name (ClassManager.cs)");
            return null;
        }
    }
}
