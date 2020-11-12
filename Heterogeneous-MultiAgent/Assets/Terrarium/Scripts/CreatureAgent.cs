using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using MLAgents;
using MLAgents.Sensor;
using Random = UnityEngine.Random;

public enum CreatureType
{
    Herbivore,
    Carnivore
}
public class CreatureAgent : Agent
{

    [Header("Creature Type")]
    public CreatureType CreatureType;
    [Header("Creature Points (100 Max)")]
    public float MaxEnergy;
    public float MatureSize;
    public float GrowthRate;
    public float EatingSpeed;
    public float DecayRate;
    public float MaxSpeed;
    public float AttackDamage;
    public float DefendDamage;
    public float Eyesight;

    [Header("Monitoring")]
    public float Energy;
    public float Size;
    public float Age;
    public float eatingCD;
    public float corpseEnergy;
    public string currentAction;

    [Header("Child")]
    public GameObject ChildSpawn;

    [Header("Species Parameters")]
    public float AgeRate = .001f;

    private GameMode gameMode;
    private Vector2 bounds;
    private GameObject Environment;
    private Rigidbody agentRB;
    private float nextAction;
    private bool died;
    private RayPerceptionSensorComponent3D rayPer;
    //private TerrariumAcademy academy;
    private int count;
    private float baseDamage, baseDefense;
    public Guid parentGUID;

    private void Awake()
    {
        InitializeAgent();
    }

    public override void AgentReset()
    {
        ResetTransform();
    }

    private void ResetTransform()
    {
        bounds = GetEnvironmentBounds();
        if (!died && name.Contains("Clone") == false)
        {
            var x = Random.Range(-bounds.x / 2, bounds.x / 2);
            var z = Random.Range(-bounds.y / 2, bounds.y / 2);
            transform.position = new Vector3(x, 1, z);
        }
    }

    public override void InitializeAgent()
    {
        base.InitializeAgent();
        if (parentGUID == Guid.Empty)
            parentGUID = Guid.NewGuid();
        Environment = GameObject.Find("Plane");
        gameMode = GetComponentInParent<GameMode>();
        rayPer = GetComponent<RayPerceptionSensorComponent3D>();
        rayPer.rayLength = Eyesight;
        agentRB = GetComponent<Rigidbody>();
        currentAction = "Idle";
        baseDamage = AttackDamage;
        baseDefense = DefendDamage;
        eatingCD = 0;
        Size = 1;
        Energy = 1;
        Age = 0.01f;
        corpseEnergy = 0;
        ResetTransform();
    }

    public override void CollectObservations()
    {
        AddVectorObs(transform.position);
        AddVectorObs(transform.position.x);
        AddVectorObs(transform.position.z);
        AddVectorObs(transform.rotation);
        AddVectorObs(bounds);
        AddVectorObs(Energy);
        AddVectorObs(Size);
        AddVectorObs(Age);
        AddVectorObs(CanEat);
        AddVectorObs(CanReproduce);
        AddVectorObs(CanAttack);
    }

    private float Float(bool val)
    {
        if (val) return 1.0f;
        else return 0.0f;
    }

    public override void AgentAction(float[] vectorAction)
    {

        //Action Space 3 int
        // 0: 1 = Eat | 2 = Reproduce | 3 = Attack | 4 = Defend | 5 = Move order
        // 1: Move
        // 2: Rotate
        switch (vectorAction[0])
        {
            case 1:
                Eat();
                break;

            case 2:
                Reproduce();
                break;

            case 3:
                if (CreatureType == CreatureType.Carnivore && CanAttack)
                    Attack();
                break;

            case 4:
                Defend();
                break;

            case 5:
                MoveAgent(vectorAction);
                break;
        }
    }

    void Defend()
    {
        currentAction = "Defend";
        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
        Energy -= 0.005f;
    }

    void Attack()
    {
        float damage = 0f;
        currentAction = "Attack";
        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
        var adj = FirstAdjacentAttackable();
        if (adj != null && FirstAdjacentAttackable().tag == "herbivore")
        {
            transform.LookAt(adj.transform);
            var vic = adj.GetComponent<CreatureAgent>();
            if (vic.currentAction == "Defend")
            {
                damage = AttackDamage - vic.DefendDamage;
                vic.AddReward(0.5f);
            }
            else
            {
                damage = AttackDamage;
            }
            CalculateEnergyLoss(damage, vic);
            AddReward(0.5f);
            Energy -= .02f;
        }
        else if (adj != null && FirstAdjacentAttackable().tag == "carnivore")
        {
            transform.LookAt(adj.transform);
            var vic = adj.GetComponent<CreatureAgent>();
            if (vic.currentAction == "Defend")
            {
                damage = AttackDamage - vic.DefendDamage;
                vic.AddReward(0.5f);
            }
            else if (vic.currentAction == "Attack")
            {
                damage = AttackDamage - vic.AttackDamage;
            }
            else
            {
                damage = AttackDamage;
            }
            CalculateEnergyLoss(damage, vic);
            AddReward(0.2f);
            Energy -= .02f;
        }
    }

    private void CalculateEnergyLoss(float damage, CreatureAgent vic)
    {
        if (damage > 0)
        {
            vic.Energy -= damage;
            if (vic.Energy < 0)
            {
                AddReward(1f);
            }
        }
        else if (damage < 0)
        {
            Energy -= damage;
        }
    }

    void Update()
    {
        MonitorLog();
        if (died)
        {
            currentAction = "Dead";
            corpseEnergy -= DecayRate;
            if (corpseEnergy < 0.1)
            {
                var go = Instantiate(ChildSpawn, transform.position, Quaternion.identity, gameObject.transform.parent);
                go.name = name;
                var ca = go.GetComponent<CreatureAgent>();
                ca.AgentReset();
                GetComponentInParent<CreatureAgentFSM>().creatureAgent = go.GetComponent<CreatureAgent>();
                GetComponentInParent<CreatureAgentFSM>().creatureAgentBodyTrans = go.transform;
                Destroy(this.gameObject);
            }
            return;
        }
        eatingCD -= EatingSpeed;
        Energy -= 0.0001f; // Existential energy loss
        if (OutOfBounds)
        {
            AddReward(-1f);
            died = true;
            Done();
            return;
        }
        //if (Buried)
        //{
        //    Done();
        //    AgentOnDone();
        //    return;
        //}

        if (Dead)
        {
            died = true;
            Done();
            return;
        }
        Grow();
        Eat();
        //Reproduce();
        Age += AgeRate;
    }

    public void FixedUpdate()
    {
        if (!died)
        {
            if (Time.timeSinceLevelLoad > nextAction)
            {
                currentAction = "Deciding";
                RequestDecision();
            }
        }
    }

    public void MonitorLog()
    {
        if (!died)
        {
            Monitor.Log("Action", currentAction, transform);
            Monitor.Log("Size", Size / MatureSize, transform);
            Monitor.Log("Energy", Energy / MaxEnergy, transform);
            Monitor.Log("Age", Age / MatureSize, transform);
        }
        else
        {
            Monitor.Log("Corpse Energy", corpseEnergy / Size, transform);
        }
    }

    public bool OutOfBounds
    {
        get
        {
            if (transform.position.y < 0) return true;
            if (transform.position.x > bounds.x ||
                transform.position.x < -bounds.x ||
                transform.position.z > bounds.y ||
                transform.position.z < -bounds.y)
                return true;
            return false;
        }
    }

    void TransformSize()
    {
        transform.localScale += (transform.localScale * 0.05f);
        transform.localScale = Vector3.one * Mathf.Clamp(transform.localScale.magnitude, 0, 2);

    }

    public bool CanGrow
    {
        get
        {
            return Energy > ((MaxEnergy / 3) + 1);
        }
    }

    public bool CanEat
    {
        get
        {
            if (eatingCD <= 0)
            {
                if (CreatureType == CreatureType.Herbivore)
                {
                    if (FirstAdjacent("plant") != null)
                        return true;
                }
                if (CreatureType == CreatureType.Carnivore)
                {
                    if (FirstAdjacentDead() != null)
                        return true;
                }
            }
            return false;
        }
    }

    public bool CanAttack
    {
        get
        {
            if (CreatureType == CreatureType.Carnivore)
            {
                if (FirstAdjacentAttackable() != null)
                    return true;
            }
            return false;
        }
    }

    private GameObject FirstAdjacentAttackable()
    {
        var colliders = Physics.OverlapSphere(transform.position, Size / 3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.GetComponent<CreatureAgent>() != null && collider.gameObject != this.gameObject && collider.gameObject.GetComponent<CreatureAgent>().parentGUID != parentGUID)
            {
                if (collider.gameObject.GetComponent<CreatureAgent>().died == false)
                    return collider.gameObject;
            }
        }
        return null;
    }

    private GameObject FirstAdjacent(string tag)
    {
        var colliders = Physics.OverlapSphere(transform.position, Size / 3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.tag == tag && collider.gameObject != this.gameObject)
            {
                if (tag != "plant" && collider.gameObject.GetComponent<CreatureAgent>().parentGUID == parentGUID)
                    return null;
                else
                    return collider.gameObject;
            }
        }
        return null;
    }

    private GameObject FirstAdjacentDead()
    {
        var colliders = Physics.OverlapSphere(transform.position, Size/3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.GetComponent<CreatureAgent>() != null)
            {
                if (collider.gameObject.GetComponent<CreatureAgent>().died)
                    return collider.gameObject;
            }
        }
        return null;
    }

    public bool CanReproduce
    {
        get
        {
            if (Size >= MatureSize && CanGrow)
                return true;
            else return false;
        }
    }

    bool Dead
    {
        get
        {
            if (Age > MatureSize || Energy < 0)
            {
                currentAction = "Dead";
                died = true;
                GetComponentInChildren<Animator>().enabled = false;
                transform.Rotate(transform.rotation.x, transform.rotation.y, 90);
                if (Energy > 0) AddReward(1f);
                else AddReward(-0.25f);
                Energy = Size;  //creature size is converted to energy
                corpseEnergy = Energy;
                return true;
            }
            return false;
        }
    }

    //bool Buried
    //{
    //    get
    //    {
    //        Energy -= AgeRate;
    //        return Energy < 0;
    //    }
    //}

    void Grow()
    {
        if (CanGrow)
        {
            if (Size > MatureSize) return;
            Energy -= Energy / 4;
            Size += GrowthRate * Random.value;
            Size = Mathf.Clamp(Size, 0, MatureSize + 1);
            DefendDamage = baseDefense + (Size / 10);
            AttackDamage = baseDamage + (Size / 10);
            nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
            currentAction = "Growing";
            TransformSize();
        }
    }

    void Reproduce()
    {
        if (CanReproduce)
        {
            //Vector3 vec =  Vector3.Min(Vector3.one, Random.insideUnitCircle * 3);
            //vec += transform.position;
            //vec = new Vector3(Mathf.Clamp(vec.x, -bounds.x + 3, bounds.x - 3), 1, Mathf.Clamp(vec.z, -bounds.y + 3, bounds.y - 3));
            //var go = Instantiate(ChildSpawn, vec, Quaternion.identity, gameObject.transform.parent);
            //if (CreatureType == CreatureType.Carnivore)
            //    go.GetComponent<CreatureAgent>().parentGUID = parentGUID;
            //go.name = go.name + (count++).ToString();
            //var ca = go.GetComponent<CreatureAgent>();
            //ca.AgentReset();
            Energy = Energy / 2;
            AddReward(1f);
            currentAction = "Reproducing";
            nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
        }
    }

    public void Eat()
    {
        if (CanEat)
        {
            currentAction = "Eating";
            if (CreatureType == CreatureType.Herbivore)
            {
                var adj = FirstAdjacent("plant");
                if (adj != null)
                {
                    try
                    {
                        transform.LookAt(adj.transform);
                        transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
                        eatingCD = 1;
                        var creature = adj.GetComponent<Plant>();
                        var consume = Mathf.Min(creature.Energy, 3);
                        creature.Energy -= consume;
                        if (creature.Energy < .1) Destroy(adj);
                        Energy += consume;
                        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
                    }
                    catch { System.Exception e; }
                }
            }
            if (CreatureType == CreatureType.Carnivore)
            {
                var adj = FirstAdjacentDead();
                if (adj != null)
                {
                    try
                    {
                        transform.LookAt(adj.transform);
                        eatingCD = 1;
                        var creature = adj.GetComponent<CreatureAgent>();
                        var consume = Mathf.Min(creature.corpseEnergy, 8f);
                        creature.corpseEnergy -= consume;
                        Energy += consume;
                        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
                    }
                    catch { System.Exception e; }
                }
            }
        }
    }

    public void MoveAgent(float[] act)
    {
        int move = 0;
        int rotate = 0;

        if (act[1] == 1)
            move = 1;

        if (act[2] == 1)
            rotate = 1;
        else if (act[2] == 2)
            rotate = -1;

        Vector3 rotateDir = Vector3.zero;
        rotateDir = transform.up * rotate;
        transform.position = transform.position + (transform.forward * move);
        transform.Rotate(rotateDir, Time.deltaTime * 10 * MaxSpeed);
        Energy -= .01f;
        currentAction = "Moving";
        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
    }

    private Vector2 GetEnvironmentBounds()
    {
        var xs = Environment.transform.localScale.x;
        var zs = Environment.transform.localScale.z;
        return new Vector2(xs, zs) * 5;
    }
}
