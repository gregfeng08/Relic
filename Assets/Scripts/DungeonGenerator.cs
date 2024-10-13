using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator : MonoBehaviour
{
    [SerializeField] GameObject circleVisualizerObj;
    [SerializeField] float degInterval;
    [SerializeField] List<GameObject> roomPrefabList;
    [SerializeField] float circleRadius;
    [SerializeField] float detectionRadius;
    [SerializeField] int numRooms;
    [SerializeField] int maxTries;

    public static DungeonGenerator inst;

    // Start is called before the first frame update
    void Start()
    {
        //Generate the outer circle
        for(float i=0;i<360;i+=degInterval)
        {
            GameObject tempObj = GameObject.Instantiate(circleVisualizerObj);
            float xPos = Mathf.Pow(circleRadius,2) * Mathf.Cos(i);
            float zPos = Mathf.Pow(circleRadius,2) * Mathf.Sin(i);
            tempObj.transform.position = new Vector3(xPos,0,zPos);
        }

        //Room Generation
        DropRoom(numRooms);
    }

    //Drop a room within the circle without overlaps
    public void DropRoom(int numRooms)
    {
        List<GameObject> placedRooms = new List<GameObject>();

        for (int i = 0; i < numRooms; i++)
        {
            bool positionFound = false;
            Vector3 randPt = Vector3.zero;
            GameObject randRoom = null;

            int currTries = 0;

            // Keep trying to find a position until it's not near any other room
            while (!positionFound)
            {
                float randTheta = Random.Range(0, 2 * Mathf.PI);
                float randX = Mathf.Pow(circleRadius, 2) * Random.Range(0.0f, 1.0f) * Mathf.Cos(randTheta);
                float randZ = Mathf.Pow(circleRadius, 2) * Random.Range(0.0f, 1.0f) * Mathf.Sin(randTheta);
                randPt = new Vector3(randX, 0, randZ);

                // Check if the position is valid
                bool tooClose = false;
                foreach (GameObject placedRoom in placedRooms)
                {
                    if (Vector3.Distance(randPt, placedRoom.transform.position) < detectionRadius)
                    {
                        tooClose = true;
                        break;
                    }
                }
                currTries++;

                // If it's not too close to any other room, we're good
                if (!tooClose)
                {
                    positionFound = true;
                    randRoom = GameObject.Instantiate(roomPrefabList[(int)(Random.Range(0.0f, roomPrefabList.Count - 1))]);
                    randRoom.transform.position = randPt;
                    placedRooms.Add(randRoom);
                    currTries = 0; //Reset 
                }

                if(currTries >= maxTries)
                {
                    positionFound = true;
                    Debug.LogWarning("Attempts Exceeded MaxTries...");
                }
            }
        }
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
