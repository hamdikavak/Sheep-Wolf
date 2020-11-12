using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Plant : MonoBehaviour {
    [Header("Plant Points (30 Max)")]    
    public float MaxEnergy;
    public float MatureSize;
    public float GrowthRate;
    public float SeedSpreadRadius;
    
    [Header("Monitoring")]
    public float Energy;
    public float Size;    
    public float Age;
    
    [Header("Seedling")]
    public GameObject SeedlingSpawn;
  
    [Header("Species Parameters")]
    public float EnergyGrowthRate = .01f;
    public float AgeRate = .001f;

    private Vector2 bounds;
    private GameObject Environment;

    private void Start()
    {
        Size = 1;
        Energy = 1;
        Age = 0;
        Environment = GameObject.Find("Plane");
        bounds = GetEnvironmentBounds();
        TransformSize();
    }
    

    void Update ()
    {        
        if (CanGrow) Grow();
        if (CanReproduce) Reproduce();
        if (Dead) Destroy(this);
        Age += AgeRate;
        Energy += EnergyGrowthRate;               
    }

    void TransformSize()
    {
        transform.localScale += (transform.localScale * 0.05f);
        transform.localScale = Vector3.one * Mathf.Clamp(transform.localScale.magnitude, 0, 2);
    }

    bool CanGrow
    {
        get
        {
            return Energy > ((MaxEnergy / 2) + 1);
        }
    }

    bool CanReproduce
    {
        get
        {
            if (Size >= MatureSize && CanGrow) return true;
            else return false;
        }
    }    

    bool Dead
    {
        get
        {
            return Energy < 0 || Age > MatureSize;
        }
    }

    void Grow()
    {
        if (Size > MatureSize) return;
        Energy = Energy / 2;
        Size += GrowthRate * Random.value;
        TransformSize();
    }

    public void Reproduce()
    {
        var vec = Random.insideUnitCircle * SeedSpreadRadius 
            + new Vector2(transform.position.x, transform.position.z);
        vec = new Vector2(Mathf.Clamp(vec.x, -bounds.x + 3, bounds.x - 3), Mathf.Clamp(vec.y, -bounds.y + 3, bounds.y - 3));
        Instantiate(SeedlingSpawn, new Vector3(vec.x,0,vec.y), Quaternion.identity, gameObject.transform.parent);
        Energy = Energy / 2;
    }

    private Vector2 GetEnvironmentBounds()
    {
        var xs = Environment.transform.localScale.x;
        var zs = Environment.transform.localScale.z;
        return new Vector2(xs, zs) * 5;
    }
}
