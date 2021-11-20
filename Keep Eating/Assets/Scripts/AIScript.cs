/********************************************************************
 * 
 * Script to control the AI
 * 
 * Clamp Values: minX, maxX, minY, maxY
 *    Small Map; -150,  152, -108,  82
 * 
 ********************************************************************/
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;



namespace Com.tuf31404.KeepEating
{
    public class AIScript : MonoBehaviour
    {

        [SerializeField]
        private bool isEater;
        //Serialized private variables
        [SerializeField]
        private float speed;
        [SerializeField]
        private Sprite shotgunSprite, revolverSprite, taserSprite;
        [SerializeField]
        private SpriteRenderer mySpriteRenderer, weaponSpriteRenderer;
        [SerializeField]
        private Shoot shootScript;
        [SerializeField]
        private Transform muzzleTransform;
        [SerializeField]
        private GameObject bulletPrefab;
        //object variables
        private PhotonTeamsManager teamsManager;
        private int bulletsShot;
        //booleans
        private bool hasGun = false;                                //change to hasWeapon
        //strings
        private Items weaponType;
        private string tempItemName;
        private Items tempFoodType;
        [Tooltip("The current Health of our player")]
        public float Health = 1f;


        private GameObject target;
        private GameObject[] enemyTargets;
        private GameObject[] itemTargets;
        private GameObject[] nodes;
        [SerializeField]
        private Transform myTransform;
        [SerializeField]
        private PhotonView thisPV;
        private int shootDistance;
        private bool canShoot;
        [SerializeField]
        private int fieldOfVisionX, fieldOfVisionY;
        private float minX, maxX, minY, maxY;
        private bool wandering;
        private Vector3 wanderTarget;
        private bool hasTarget, newWander;
        private bool gameStart;
        private BotMap botMap;
        public PhotonView PV { get; set; }
        public bool IsAlive { get; set; }

        public bool isAlpha = false;

        public bool inDijkstra = false;

        private int[] shortestPath;
        private int pathCounter = 1;
        int dijkstraTarget;
        int currentNode = -1;


        private void Start()
        {
            teamsManager = GameObject.Find("Team Manager(Clone)").GetComponent<PhotonTeamsManager>();
            SetTargets();
            shootDistance = 0;
            canShoot = true;
            wandering = false;
            hasTarget = false;
            newWander = true;
            wanderTarget = Vector3.zero;
            gameStart = false;
            target = null;
            IsAlive = true;
            if (StaticSettings.Map.Equals("SmallGameMap"))
            {
                minX = -150;
                maxX = 152;
                minY = -108;
                maxY = 82;
            }
            else
            {
                minX = -250f;
                maxX = 250f;
                minY = -235f;
                maxY = 235f;
            }
            Debug.Log("Nodes.Length = " + nodes.Length);
            botMap = new BotMap(nodes.Length);
            shortestPath = new int[36];
            //SetBotMap();
            //botMap.PrintMap();
            StartCoroutine("WaitSetBotMap");
            StartCoroutine("StartWaiter");
        }


        // Update is called once per frame
        void Update()
        {
            if (gameStart && IsAlive)
            {
                if (!isAlpha)
                {
                    if (!isEater)
                    {
                        //Debug.Log("has target = " + hasTarget + " has gun = " + hasGun + " wandering = " + wandering);
                    }
                    if (!hasTarget)
                    {
                        target = GetTarget();
                    }
                    else
                    {
                        if (TargetInView(target.transform.position))
                        {
                            if (target.tag.Equals("Player") || target.tag.Equals("EaterAI"))
                            {
                                if (!target.transform.GetChild(1).gameObject.GetComponent<SpriteRenderer>().enabled)
                                {
                                    target = GetTarget();
                                }
                            }
                            else if (!target.GetComponent<SpriteRenderer>().enabled)
                            {
                                target = GetTarget();
                            }
                        }
                        else
                        {
                            target = GetTarget();
                        }
                    }

                    if (target != null)
                    {

                        if (!isEater)
                        {
                            //Debug.Log("Target name = " + target.name);
                        }
                        hasTarget = true;
                        wandering = false;
                        if ((target.tag.Equals("Player") || target.tag.Equals("EaterAI")) && hasGun && target.transform.GetChild(1).gameObject.GetComponent<SpriteRenderer>().enabled)
                        {
                            if (TargetDistance(target.transform.position) <= shootDistance && canShoot)
                            {
                                if (weaponType == Items.Shotgun)
                                {
                                    for (int i = 0; i < 5; i++)
                                    {
                                        bulletsShot++;
                                        this.PV.RPC("ShootGun", RpcTarget.All, PhotonNetwork.NickName, shootScript.ShootGun(weaponType, target.transform.position), muzzleTransform.position);
                                    }
                                }
                                else
                                {
                                    bulletsShot++;
                                    this.PV.RPC("ShootGun", RpcTarget.All, PhotonNetwork.NickName, shootScript.ShootGun(weaponType, target.transform.position), muzzleTransform.position);
                                }
                                StartCoroutine("ShootWaiter");
                            }
                        }

                    }
                    else
                    {
                        hasTarget = false;
                        if (newWander || myTransform.position == wanderTarget)
                        {
                            wanderTarget = Wander();
                        }
                        wandering = true;
                    }
                    Move(wandering);
                }
                else
                {
                    if (!inDijkstra)
                    {
                        Debug.Log("In Dijkstra");
                        pathCounter = 1;
                        for (int i = 0; i < shortestPath.Length; i++)
                        {
                            shortestPath[i] = -1;
                        }
                        MoveDjikstra();
                    }
                    else
                    {
                        if (shortestPath[pathCounter] != -1)
                        {
                            if (TargetDistance(nodes[shortestPath[pathCounter]].transform.position) == 0)
                            {
                                Debug.Log("Node = " + shortestPath[pathCounter]);
                                currentNode = shortestPath[pathCounter];
                                pathCounter++;
                            }
                        }
                        else
                        {
                            inDijkstra = false;
                        }
                        if (pathCounter <= 35)
                        {
                            if (shortestPath[pathCounter] != -1)
                            {
                                float step = speed * Time.deltaTime;
                                myTransform.position = Vector3.MoveTowards(myTransform.position, nodes[shortestPath[pathCounter]].transform.position, step);
                                myTransform.position = new Vector3(
                            Mathf.Clamp(myTransform.position.x, minX, maxX),
                            Mathf.Clamp(myTransform.position.y, minY, maxY),
                            0.0f);
                            }
                        }
                        else
                        {
                            inDijkstra = false;
                        }
                    }

                }
            }
            if (Health <= 0f && IsAlive)
            {
                //GameManager.Instance.LeaveRoom();
                IsAlive = false;
                PV.RPC("PlayerDead", RpcTarget.All, thisPV.ViewID);
                IEnumerator coroutine = RespawnWaiter(thisPV.ViewID);
                StartCoroutine(coroutine);
            }
        }
        
        void Move(bool _isWandering)
        {
            float step = speed * Time.deltaTime;
            if (_isWandering)
            {
                myTransform.position = Vector3.MoveTowards(myTransform.position, wanderTarget, step);
            }
            else
            {
                myTransform.position = Vector3.MoveTowards(myTransform.position, target.transform.position, step);
            }

            myTransform.position = new Vector3(
                    Mathf.Clamp(myTransform.position.x, minX, maxX),
                    Mathf.Clamp(myTransform.position.y, minY, maxY),
                    0.0f);
        }

        void MoveDjikstra()
        {
            inDijkstra = true;
            if (currentNode == -1)
            {
                int closestNode = -1;
                float prev = Mathf.Infinity;
                for (int i = 0; i < nodes.Length; i++)
                {
                    float temp = TargetDistance(nodes[i].transform.position);
                    if (temp < prev)
                    {
                        if (TryRayCast(this.gameObject.transform.position, nodes[i].transform.position))
                        {
                            prev = temp;
                            closestNode = i;
                        }
                    }
                }
                if (closestNode == -1)
                {
                    Debug.Log("CLOSEST NODE ERROR");
                }
                currentNode = closestNode;
            }

            int targetNode;
            while ((targetNode = UnityEngine.Random.Range(0,36)) == currentNode){ }
            Debug.Log("Current position = " + this.gameObject.transform.position);
            Debug.Log("Current node = " + nodes[currentNode].name);
            Debug.Log("target node = " + nodes[targetNode].name);
            int[] tempPath = botMap.Dijkstra(currentNode, targetNode);
            shortestPath[0] = currentNode;
            for (int i = tempPath.Length-1; i >= 0; i--)
            {
                if (tempPath[i] != -1)
                {
                    shortestPath[pathCounter++] = tempPath[i];
                }
            }
            string print = "shortestPath = ";
            for (int i = 0; i < shortestPath.Length; i++)
            {
                print += shortestPath[i] + " ";
            }
            Debug.Log(print);
            pathCounter = 0;
            
        }

        GameObject GetTarget()
        {
            GameObject retTarget = null;
            float targetDistance = 10000f;
            float tempDistance = 0;

            if (isEater)
            {
                foreach (GameObject item in itemTargets)
                {
                    if (item != null && item.GetComponent<SpriteRenderer>().enabled)
                    {
                        tempDistance = TargetDistance(item.transform.position);
                        if (tempDistance < targetDistance)
                        {
                            targetDistance = tempDistance;
                            retTarget = item;
                        }
                    }
                }
            }
            else
            {
                if (!this.hasGun)
                {
                    foreach (GameObject item in itemTargets)
                    {
                        if (item != null && item.GetComponent<SpriteRenderer>().enabled)
                        {
                            tempDistance = TargetDistance(item.transform.position);
                            if (tempDistance < targetDistance)
                            {
                                targetDistance = tempDistance;
                                retTarget = item;
                            }
                        }
                    }
                }
                else
                {
                    foreach (GameObject item in enemyTargets)
                    {
                        if (item != null && item.transform.GetChild(1).gameObject.GetComponent<SpriteRenderer>().enabled)
                        {
                            tempDistance = TargetDistance(item.transform.position);
                            if (tempDistance < targetDistance)
                            {
                                targetDistance = tempDistance;
                                retTarget = item;
                            }
                        }
                    }
                }
            }

            return retTarget;
        }
        
        Vector3 Wander()
        {
            float xPos = UnityEngine.Random.Range(-150, 152);
            float yPos = UnityEngine.Random.Range(-108, 82);
            StartCoroutine("WanderWaiter");
            return new Vector3(xPos, yPos, 0f);
        }
        void SetTargets()
        {
            if (isEater)
            {
                itemTargets = GameObject.FindGameObjectsWithTag("Food");
            }
            else
            {
                itemTargets = GameObject.FindGameObjectsWithTag("Weapon");
                //Debug.Log("weapons size = " + itemTargets.Length);
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                GameObject[] eaterAI = GameObject.FindGameObjectsWithTag("EaterAI");
                enemyTargets = new GameObject[players.Length + eaterAI.Length];
                int index = 0;
                foreach (GameObject player in players)
                {
                    if (player.GetComponent<PlayerManagerV2>().MyTeam == 1)
                    {

                        Debug.Log("Adding Enemy targets");
                        enemyTargets[index++] = player;
                    }
                }
                foreach (GameObject eater in eaterAI)
                {
                    enemyTargets[index++] = eater;
                }
            }
            nodes = new GameObject[36];

            string n = "Node";
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = GameObject.Find(n + i);
            }
        }

        float TargetDistance(Vector3 targetPos)
        {
            if (TargetInView(targetPos)){
                return Mathf.Sqrt(Mathf.Pow(targetPos.x - myTransform.position.x, 2) + Mathf.Pow(targetPos.y - myTransform.position.y, 2));
            }
            else
            {
                return 10001f;
            }
        }

        bool TargetInView(Vector3 targetPos)
        {
            float distX = Mathf.Abs(targetPos.x - myTransform.position.x);
            float distY = Mathf.Abs(targetPos.y - myTransform.position.y);
            if (distX < fieldOfVisionX && distY < fieldOfVisionY)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.name.Contains("Weapon") && !hasGun && !isEater)
            {
                tempItemName = other.gameObject.name;
                weaponType = other.gameObject.GetComponent<ItemSpawnScript>().ItemType;
                hasGun = true;
                this.PV.RPC("PickUpGun", RpcTarget.All, thisPV.ViewID, weaponType, tempItemName);
                if (weaponType == Items.Shotgun)
                {
                    shootDistance = 60;
                }
                else
                {
                    shootDistance = 90;
                }
                hasTarget = false;
                target = null;
            }
            else if (other.name.Contains("Food") && isEater)
            {
                tempItemName = other.gameObject.name;
                tempFoodType = other.gameObject.GetComponent<ItemSpawnScript>().ItemType;
                this.PV.RPC("PickUpFood", RpcTarget.All, tempItemName, tempFoodType);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Bullet") && isEater)
            {
                Health -= 0.3f;
            }
        }
        IEnumerator ShootWaiter()
        {
            canShoot = false;
            yield return new WaitForSeconds(1);
            canShoot = true;
        }

        IEnumerator StartWaiter()
        {
            yield return new WaitForSeconds(3);
            gameStart = true;
        }

        IEnumerator WanderWaiter()
        {
            newWander = false;
            float waitTime = UnityEngine.Random.Range(0, 10);
            yield return new WaitForSeconds(waitTime);
            newWander = true;
        }

        IEnumerator WaitSetBotMap()
        {
            SetBotMap();
            Debug.Log("Bot Map Set");
            yield return null; 
        }

        IEnumerator RespawnWaiter(int pvId)
        {
            yield return new WaitForSeconds(10f);
            GameObject[] spawns = GameObject.FindGameObjectsWithTag("EaterSpawn");
            if (spawns.Length != 0)
            {
                int spawnPoint = UnityEngine.Random.Range(0, spawns.Length);
                PV.RPC("PlayerRespawn", RpcTarget.All, pvId, spawns[spawnPoint].transform.position);
                GameObject.FindWithTag("GSM").GetComponent<GameStateManager>().PlayerRespawn();
            }
            else
            {
                PV.RPC("PlayerRespawn", RpcTarget.All, pvId, Vector3.zero);
            }
        }

        private void SetBotMap()
        {
            Vector3 a, b;
            for (int i = 0; i < nodes.Length; i++)
            {
                for (int j = 0; j < nodes.Length; j++)
                {
                    if (i != j)
                    {
                        a = nodes[i].transform.position;
                        b = nodes[j].transform.position;
                        if (TryRayCast(a, b))
                        {
                            botMap.Add(a, b, i, j);
                        }
                    }
                }
            }
            if (isAlpha)
            {
                //botMap.PrintMap(nodes);
                //botMap.Dijkstra(1, 20);
            }
        }

        private bool TryRayCast(Vector3 _a, Vector3 _b)
        {
            float dist = Mathf.Sqrt(Mathf.Pow(_a.x - _b.x, 2) + Mathf.Pow(_a.y - _b.y, 2));
            Vector2 direction = new Vector2(_b.x - _a.x, _b.y - _a.y).normalized;
            Vector2 a = new Vector2(_a.x, _a.y);
            LayerMask mask = LayerMask.GetMask("Wall");
            RaycastHit2D hit = Physics2D.Raycast(a, direction, dist, mask);
         
            if (hit.collider != null)
            {
                if (isAlpha)
                {
                    Debug.Log(hit.collider.gameObject.tag);
                }
                if (hit.collider.gameObject.CompareTag("Wall"))
                {
                    return false;
                }
                else
                {
                    Debug.Log("somethin wrong");
                }
            }
            return true;
        }
    }
}


