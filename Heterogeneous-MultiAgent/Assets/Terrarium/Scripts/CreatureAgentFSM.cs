using MLAgents;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

//public enum CreatureType
//{
//    Herbivore,
//    Carnivore
//}

public class CreatureAgentFSM : Agent
{
    [Header("Creature Type")]
    public CreatureType creatureType;
    [SerializeField] private LayerMask _layerMask;
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
    public string currentState;

    [Header("Child")]
    public GameObject ChildSpawn;

    [Header("Species Parameters")]
    public float AgeRate = .001f;

    public Guid parentGUID;
    private GameMode gameMode;
    private Vector2 bounds;
    private GameObject Environment;
    private Rigidbody agentRB;
    private float nextAction;
    private bool died;
    private int count;
    private float baseDamage, baseDefense;
    private CreatureState _currentState;
    private Vector3 _destination;
    private float _stoppingDistance = 1.5f;
    private float _rayDistance = 10.0f;
    private Quaternion _desiredRotation;
    private Vector3 _direction;
    private GameObject _target;
    public CreatureAgent creatureAgent;
    public Transform creatureAgentBodyTrans;
    private GameObject oldTarget;
    private bool Reproduced;

    private void Start()
    {
        creatureAgent = GetComponentInChildren<CreatureAgent>();
        creatureAgentBodyTrans = transform.GetChild(0);
        if (parentGUID == Guid.Empty)
            parentGUID = Guid.NewGuid();
        //if (creatureType == CreatureType.Carnivore)
        //    _rayDistance = 10f;
        Environment = GameObject.Find("Plane");
        gameMode = GetComponentInParent<GameMode>();
        agentRB = GetComponentInChildren<Rigidbody>();
        _currentState = CreatureState.Move;
        baseDamage = AttackDamage;
        baseDefense = DefendDamage;
        eatingCD = 0;
        Size = 1;
        Energy = 1;
        Age = 0.01f;
        corpseEnergy = 0;
        ResetTransform();
        creatureAgentBodyTrans = gameObject.transform.GetChild(0);
    }


    void Defend()
    {
        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
        Energy -= 0.005f;
        if (Energy < 0.1)
            _currentState = CreatureState.Dead;
    }

    void Attack()
    {
        float damage = 0f;
        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
        var adj = FirstAdjacentAttackable();
        if (adj != null && adj.transform.GetChild(0).gameObject.tag == "herbivore")
        {
            //creatureAgentBodyTrans.LookAt(adj.transform);
            var vic = adj.GetComponent<CreatureAgentFSM>();
            if (vic._currentState == CreatureState.Defend)
            {
                damage = AttackDamage - vic.DefendDamage;
            }
            else
            {
                damage = AttackDamage;
            }
            CalculateEnergyLoss(damage, vic);
            Energy -= .02f;
        }
        else if (adj != null && adj.transform.GetChild(0).gameObject.tag == "carnivore")
        {
            //creatureAgentBodyTrans.LookAt(adj.transform);
            var vic = adj.GetComponent<CreatureAgentFSM>();
            if (vic._currentState == CreatureState.Defend)
            {
                damage = AttackDamage - vic.DefendDamage;
            }
            else if (vic._currentState == CreatureState.Attack)
            {
                damage = AttackDamage - vic.AttackDamage;
            }
            else
            {
                damage = AttackDamage;
            }
            CalculateEnergyLoss(damage, vic);
            Energy -= .02f;
        }
        if (CanEat) _currentState = CreatureState.Eat;
    }

    private void CalculateEnergyLoss(float damage, CreatureAgentFSM vic)
    {
        if (damage > 0)
        {
            vic.Energy -= damage;
        }
        else if (damage < 0)
        {
            Energy -= damage;
        }
    }
    public override void CollectObservations()
    {
        AddVectorObs(CanEat);
        AddVectorObs(CanReproduce);
        AddVectorObs(CanGrow);
        AddVectorObs(CanAttack);
        AddVectorObs(Energy);
        AddVectorObs(MaxEnergy);
        AddVectorObs(Size);
        AddVectorObs(Age);
        AddVectorObs(creatureType == CreatureType.Carnivore ? 1 : 0);
        if (creatureType == CreatureType.Carnivore)
            AddVectorObs(CheckForAggro() == null ? 0 : 1);
        if (creatureType == CreatureType.Herbivore)
            AddVectorObs(CheckForPlants() == null ? 0 : 1);
    }

    public override void AgentAction(float[] vectorAction)
    {
        var action = vectorAction[0];
        if (Dead) return;
        switch (action)
        {
            case 0:
                _currentState = CreatureState.Idle;
                break;
            case 1:
                if (creatureType == CreatureType.Carnivore)
                {
                    _currentState = CreatureState.Attack;
                }
                break;
            case 2:
                _currentState = CreatureState.Eat;
                break;
            case 3:
                _currentState = CreatureState.Move;
                break;
        }
    }

    void Update()
    {
        eatingCD -= EatingSpeed;
        Energy -= 0.0001f; // Existential energy loss
        if (OutOfBounds)
        {
            died = true;
            Destroy(this.gameObject);
            return;
        }

        if (Dead)
        {
            currentState = "Dead";
            corpseEnergy -= DecayRate;
            if (corpseEnergy < 0.1)
                Destroy(gameObject);
            return;
        }

        switch (_currentState)
        {
            case CreatureState.Move:
                {
                    currentState = "Moving";

                    if (creatureType == CreatureType.Herbivore)
                    {
                        if (CanEat || (FirstAdjacent("plant") != null))
                        {
                            _currentState = CreatureState.Eat;
                            break;
                        }
                        CheckForPlants();
                    }
                    else
                    {
                        if (CanEat || (FirstAdjacentDead() != null))
                        {
                            _currentState = CreatureState.Eat;
                            break;
                        }
                        CheckForAggro();
                    }

                    if (_target != null && oldTarget != _target)
                    {
                        var plantPosFix = new Vector3(_target.transform.position.x, 1f, _target.transform.position.z);
                        creatureAgentBodyTrans.LookAt(plantPosFix);
                        creatureAgentBodyTrans.Translate(Vector3.forward * Time.deltaTime * MaxSpeed);
                        oldTarget = _target;
                        break;
                    }

                    if (NeedsDestination())
                    {
                        GetDestination();
                    }

                    creatureAgentBodyTrans.rotation = _desiredRotation;

                    creatureAgentBodyTrans.Translate(Vector3.forward * Time.deltaTime * MaxSpeed);

                    var rayColor = IsPathBlocked() ? Color.red : Color.green;
                    Debug.DrawRay(creatureAgentBodyTrans.position, _direction * _rayDistance, rayColor);

                    while (IsPathBlocked())
                    {
                        Debug.Log("Path Blocked");
                        GetDestination();
                    }
                    break;
                }

            case CreatureState.Attack:
                {
                    currentState = "Attacking";
                    Attack();
                    break;
                }

            case CreatureState.Defend:
                {
                    currentState = "Defending";
                    Defend();
                    break;
                }


            //    //case CreatureState.Dead:
            //    //    {
            //    //        currentState = "Dead";
            //    //        corpseEnergy -= DecayRate;
            //    //        if (corpseEnergy < 0.1)
            //    //            Destroy(gameObject);
            //    //        break;
            //    //    }

            case CreatureState.Growing:
                {
                    currentState = "Growing";
                    Grow();
                    break;
                }

            case CreatureState.Eat:
                {
                    Eat();
                    break;
                }

            case CreatureState.Reproducing:
                {
                    currentState = "Reproducing";
                    if (CanReproduce)
                        Reproduce();
                    break;
                }
            case CreatureState.Idle:
                {
                    break;
                }
        }

        if (CanGrow) Grow();
        if (CanReproduce) Reproduce();
        Age += AgeRate;
        MonitorLog();
    }

    public void MonitorLog()
    {
        if (!died)
        {
            Monitor.Log("Action", currentState, creatureAgentBodyTrans);
            Monitor.Log("Size", Size / MatureSize, creatureAgentBodyTrans);
            Monitor.Log("Energy", Energy / MaxEnergy, creatureAgentBodyTrans);
            Monitor.Log("Age", Age / MatureSize, creatureAgentBodyTrans);
        }
        else
        {
            Monitor.Log("Corpse Energy", corpseEnergy / Size, creatureAgentBodyTrans);
        }
    }

    public bool OutOfBounds
    {
        get
        {
            if (creatureAgentBodyTrans.position.y < 0)
                return true;
            if (creatureAgentBodyTrans.position.x > bounds.x ||
                creatureAgentBodyTrans.position.x < -bounds.x ||
                creatureAgentBodyTrans.position.z > bounds.y ||
                creatureAgentBodyTrans.position.z < -bounds.y)
                return true;
            return false;
        }
    }

    void TransformSize()
    {
        creatureAgentBodyTrans.localScale += (transform.localScale * 0.05f);
        creatureAgentBodyTrans.localScale = Vector3.one * Mathf.Clamp(transform.localScale.magnitude, 0, 2);

    }

    bool CanGrow
    {
        get
        {
            return Energy > ((MaxEnergy / 3) + 1);
        }
    }

    bool CanEat
    {
        get
        {
            if (eatingCD <= 0)
            {
                if (creatureType == CreatureType.Herbivore)
                {
                    if (FirstAdjacent("plant") != null)
                        return true;
                }
                if (creatureType == CreatureType.Carnivore)
                {
                    if (FirstAdjacentDead() != null)
                        return true;
                }
            }
            return false;
        }
    }

    bool CanAttack
    {
        get
        {
            if (creatureType == CreatureType.Carnivore)
            {
                if (FirstAdjacentAttackable() != null)
                    return true;
            }
            return false;
        }
    }

    private GameObject FirstAdjacentAttackable()
    {
        var colliders = Physics.OverlapSphere(creatureAgentBodyTrans.position, Size / 3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.GetComponentInParent<CreatureAgentFSM>() != null && collider.gameObject != this.gameObject && collider.gameObject.GetComponentInParent<CreatureAgentFSM>().parentGUID != parentGUID)
            {
                if (collider.gameObject.GetComponentInParent<CreatureAgentFSM>().died == false)
                    return collider.gameObject.transform.parent.gameObject;
            }
        }
        return null;
    }

    private GameObject FirstAdjacent(string tag)
    {
        var colliders = Physics.OverlapSphere(creatureAgentBodyTrans.position, Size / 3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.tag == tag && collider.gameObject != this.gameObject)
            {
                if (tag != "plant" && collider.gameObject.GetComponentInParent<CreatureAgentFSM>().parentGUID == parentGUID)
                    return null;
                else if (tag != "plant")
                    return collider.gameObject.transform.parent.gameObject;
                else return collider.gameObject.gameObject;
            }
        }
        return null;
    }

    private GameObject FirstAdjacentDead()
    {
        var colliders = Physics.OverlapSphere(creatureAgentBodyTrans.position, Size / 3);
        foreach (var collider in colliders)
        {
            if (collider.gameObject.GetComponentInParent<CreatureAgentFSM>() != null)
            {
                if (collider.gameObject.GetComponentInParent<CreatureAgentFSM>().died)
                    return collider.gameObject.transform.parent.gameObject;
            }
        }
        return null;
    }

    bool CanReproduce
    {
        get
        {
            if (Size >= MatureSize && CanGrow && !Reproduced)
                return true;
            else return false;
        }
    }

    bool Dead
    {
        get
        {
            if (Age > MatureSize || Energy < 0.1)
            {
                died = true;
                GetComponentInChildren<Animator>().enabled = false;
                creatureAgentBodyTrans.Rotate(creatureAgentBodyTrans.rotation.x, creatureAgentBodyTrans.rotation.y, 90);
                Energy = Size;  //creature size is converted to energy
                corpseEnergy = Energy;
                Energy = 0;
                return true;
            }
            return false;
        }
    }

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
            TransformSize();
        }
    }

    IEnumerator ReproduceDelay()
    {
        yield return new WaitForSeconds(7);
        Reproduced = false;
    }

    void Reproduce()
    {
        if (CanReproduce)
        {
            Vector3 vec = Vector3.Min(Vector3.one, Random.insideUnitCircle * 3);
            vec += creatureAgentBodyTrans.position;
            vec = new Vector3(Mathf.Clamp(vec.x, -bounds.x + 3, bounds.x - 3), 1, Mathf.Clamp(vec.z, -bounds.y + 3, bounds.y - 3));
            var go = Instantiate(ChildSpawn, vec, Quaternion.identity, transform.parent);
            if (creatureType == CreatureType.Carnivore)
                go.GetComponent<CreatureAgentFSM>().parentGUID = parentGUID;
            go.name = go.name + (count++).ToString();
            var ca = go.GetComponent<CreatureAgentFSM>();
            Energy = Energy / 2;
            nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
            Reproduced = true;
            StartCoroutine(ReproduceDelay());
        }
    }

    public void Eat()
    {
        if (CanEat)
        {
            if (creatureType == CreatureType.Herbivore)
            {
                var adj = FirstAdjacent("plant");
                if (adj != null)
                {
                    try
                    {
                        //creatureAgentBodyTrans.LookAt(adj.transform);
                        creatureAgentBodyTrans.eulerAngles = new Vector3(0, creatureAgentBodyTrans.eulerAngles.y, 0);
                        eatingCD = 1;
                        var creature = adj.GetComponent<Plant>();
                        var consume = Mathf.Min(creature.Energy, 2);
                        creature.Energy -= consume;
                        if (creature.Energy < .1) Destroy(adj);
                        Energy += consume;
                        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
                    }
                    catch { System.Exception e; }
                }
            }
            if (creatureType == CreatureType.Carnivore)
            {
                var adj = FirstAdjacentDead();
                if (adj != null)
                {
                    try
                    {
                        //creatureAgentBodyTrans.LookAt(adj.transform);
                        eatingCD = 1;
                        var creature = adj.GetComponent<CreatureAgentFSM>();
                        var consume = Mathf.Min(creature.corpseEnergy, 8f);
                        creature.corpseEnergy -= consume;
                        if (creature.corpseEnergy < .1) Destroy(adj);
                        Energy += consume;
                        nextAction = Time.timeSinceLevelLoad + (25 / MaxSpeed);
                    }
                    catch { System.Exception e; }
                }
            }
        }
    }

    public override void AgentReset()
    {
        gameMode.GameReset();
    }

    private Vector2 GetEnvironmentBounds()
    {
        var xs = Environment.transform.localScale.x;
        var zs = Environment.transform.localScale.z;
        return new Vector2(xs, zs) * 5;
    }

    private void ResetTransform()
    {
        bounds = GetEnvironmentBounds();
        if (!died && name.Contains("Clone") == false)
        {
            var x = Random.Range(-bounds.x / 2, bounds.x / 2);
            var z = Random.Range(-bounds.y / 2, bounds.y / 2);
            creatureAgentBodyTrans.position = new Vector3(x, 1, z);
        }
    }

    private bool NeedsDestination()
    {
        if (_destination == Vector3.zero)
            return true;

        var distance = Vector3.Distance(creatureAgentBodyTrans.position, _destination);
        if (distance <= _stoppingDistance)
        {
            return true;
        }

        return false;
    }

    private void GetDestination()
    {
        Vector3 testPosition = (creatureAgentBodyTrans.position + (creatureAgentBodyTrans.forward * 4f)) +
                               new Vector3(UnityEngine.Random.Range(-4.5f, 4.5f), 0f,
                                   UnityEngine.Random.Range(-4.5f, 4.5f));

        _destination = new Vector3(testPosition.x, 1f, testPosition.z);

        _direction = Vector3.Normalize(_destination - creatureAgentBodyTrans.position);
        _direction = new Vector3(_direction.x, 0f, _direction.z);
        _desiredRotation = Quaternion.LookRotation(_direction);
    }

    private bool IsPathBlocked()
    {
        Ray ray = new Ray(creatureAgentBodyTrans.position, _direction);
        var hitSomething = Physics.RaycastAll(ray, _rayDistance, _layerMask);
        Debug.DrawRay(creatureAgentBodyTrans.position, _direction, Color.red);
        return hitSomething.Any();
    }


    Quaternion startingAngle = Quaternion.AngleAxis(-60, Vector3.up);
    Quaternion stepAngle = Quaternion.AngleAxis(5, Vector3.up);

    private Transform CheckForPlants()
    {
        float scanRadius = 5f;

        RaycastHit hit;
        var angle = creatureAgentBodyTrans.rotation * startingAngle;
        var direction = angle * Vector3.forward;
        var pos = creatureAgentBodyTrans.position;
        for (var i = 0; i < 24; i++)
        {
            if (Physics.Raycast(pos, direction, out hit, scanRadius))
            {
                var plant = hit.collider.GetComponent<Plant>();
                if (plant != null)
                {
                    Debug.DrawRay(pos, direction * hit.distance, Color.red);
                    _target = plant.gameObject;
                    return plant.transform;
                }
                else
                {
                    Debug.DrawRay(pos, direction * hit.distance, Color.yellow);
                }
            }
            else
            {
                Debug.DrawRay(pos, direction * scanRadius, Color.white);
            }
            direction = stepAngle * direction;
        }

        return null;
    }

    private Transform CheckForAggro()
    {
        float aggroRadius = 10f;

        RaycastHit hit;
        var angle = creatureAgentBodyTrans.rotation * startingAngle;
        var direction = angle * Vector3.forward;
        var pos = creatureAgentBodyTrans.position;
        for (var i = 0; i < 24; i++)
        {
            if (Physics.Raycast(pos, direction, out hit, aggroRadius))
            {
                var creature = hit.collider.GetComponentInParent<CreatureAgentFSM>();
                if (creature != null && creature.parentGUID != parentGUID)
                {
                    Debug.DrawRay(pos, direction * hit.distance, Color.red);
                    _target = creature.creatureAgentBodyTrans.gameObject;
                    return creature.creatureAgentBodyTrans;
                }
                else
                {
                    Debug.DrawRay(pos, direction * hit.distance, Color.yellow);
                }
            }
            else
            {
                Debug.DrawRay(pos, direction * aggroRadius, Color.white);
            }
            direction = stepAngle * direction;
        }

        return null;
    }
    public enum CreatureState
    {
        Idle,
        Move,
        Chase,
        Eat,
        Attack,
        Defend,
        Reproducing,
        Growing,
        Dead
    }
}
