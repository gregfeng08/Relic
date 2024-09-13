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
        for(int i=0;i<numRooms;i++)
        {
            float randTheta = Random.Range(0, 2*Mathf.PI);
            float randX = Mathf.Pow(circleRadius,2)*Random.Range(0.0f, 1.0f) * Mathf.Cos(randTheta);
            float randZ = Mathf.Pow(circleRadius,2)*Random.Range(0.0f, 1.0f) * Mathf.Sin(randTheta);
            Vector3 randPt = new Vector3(randX,0,randZ);
            GameObject randRoom = GameObject.Instantiate(roomPrefabList[(int)(Random.Range(0.0f, roomPrefabList.Count-1))]); //Make this rand
            randRoom.transform.position = randPt;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
