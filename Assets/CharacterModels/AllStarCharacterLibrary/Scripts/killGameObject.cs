using UnityEngine;
using System.Collections;
#pragma warning disable

public class killGameObject : MonoBehaviour 
{
	public float lifespan;
	float startTime;

	// Use this for initialization
	void Start () 
	{
		startTime = Time.fixedTime;
	}
	
	// Update is called once per frame
	void Update () 
	{
		if((startTime+lifespan)< Time.fixedTime)
		{
			Destroy(gameObject);
		}
	}
}
