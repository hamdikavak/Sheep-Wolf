using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMode : MonoBehaviour
{
    [Header("Actors Directories")]
    public GameObject plantsDir;
    public GameObject herbivoreDir;
    public GameObject carnivoreDir;

    [Header("Spawns Prefabs")]
    public GameObject PlantPref;
    public GameObject HerbivorePref;
    public GameObject CarnivorePref;

    [Header("Game Reset Parameters")]
    public int plantSpawnCount;
    public int herbivoreSpawnCount;
    public int carnivoreSpawnCount;

    public TerrariumAcademy academy;

    private void Start()
    {
        StartCoroutine("GameResetCheck");
    }

    [Header("Environment")]
    public GameObject Environment;

    private Vector2 bounds;

    IEnumerator GameResetCheck()
    {
        if (herbivoresDead || (carnivoresDead && plantsDead))
        {
            StartCoroutine("CallAgentDone");
            yield return new WaitForSeconds(1f);
            GameReset();
        }

        yield return new WaitForSeconds(5f);
        StartCoroutine("GameResetCheck");
    }

    IEnumerator CallAgentDone()
    {
        var agents = GetComponentsInChildren<CreatureAgent>();
        foreach (CreatureAgent CA in agents)
        {
            CA.Done();
        }
        yield return new WaitForSeconds(1f);
    }

    public void GameReset()
    {
        DestroyActors(plantsDir);
        //DestroyActors(herbivoreDir);
        //DestroyActors(carnivoreDir);

        InstatiateActors(PlantPref, plantsDir, plantSpawnCount);
        //InstatiateActors(HerbivorePref, herbivoreDir, herbivoreSpawnCount);
        //InstatiateActors(CarnivorePref, carnivoreDir, carnivoreSpawnCount);
    }

    private void InstatiateActors(GameObject ActorToSpawn, GameObject ActorParentDir, int SpawnCount)
    {
        for (int i = 0; i < SpawnCount; i++)
        {
            bounds = GetEnvironmentBounds();
            var x = Random.Range(-bounds.x + 5, bounds.x - 5);
            var z = Random.Range(-bounds.y + 5, bounds.y - 5);
            float y = 1;
            if (ActorToSpawn == PlantPref)
                y = 0;
            var newActor = Instantiate(ActorToSpawn, new Vector3(x, y, z), Quaternion.identity, ActorParentDir.transform);
            
        }
    }

    private void DestroyActors(GameObject ActorDir)
    {
        foreach (Transform child in ActorDir.transform)
        {
            Destroy(child.gameObject);
        }
    }

    private Vector2 GetEnvironmentBounds()
    {
        var xs = Environment.transform.localScale.x;
        var zs = Environment.transform.localScale.z;
        return new Vector2(xs, zs) * 5;
    }

    bool plantsDead
    {
        get
        {
            return plantsDir.transform.childCount == 0;
        }
    }

    bool herbivoresDead
    {
        get
        {
            return herbivoreDir.transform.childCount == 0;
        }
    }

    bool carnivoresDead
    {
        get
        {
            return carnivoreDir.transform.childCount == 0;
        }
    }
}
