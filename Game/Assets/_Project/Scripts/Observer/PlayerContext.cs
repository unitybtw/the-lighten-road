using UnityEngine;
using System;
using System.Collections.Generic;


public class PlayerContext : MonoBehaviour
{

    public float maxHealth = 100;
    public float damage = 10;

    public List<string> abilities = new List<string>();
    public List<string> cosmetics = new List<string>();
    public int metaCurrency = 0;

    public event Action Changed;

    public void AddMaxHealth(float amount) {maxHealth += amount; Changed?.Invoke();}
    public void AddDamage(float amount) {damage += amount; Changed?.Invoke();}

    public void UnlockAbility(string id){
        if(!abilities.Contains(id)){abilities.Add(id); Changed?.Invoke(); }

    }

    public void UnlockCosmetic(string id){
        if(!cosmetics.Contains(id)) {cosmetics.Add(id); Changed?.Invoke(); }
    }

    public void AddMetaCurrency(int amount){ metaCurrency += amount; Changed?.Invoke(); }

    public void ResetCharacterState(){
        maxHealth = 100f;
        damage = 10f;
        abilities.Clear();
        Changed?.Invoke();
    }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
