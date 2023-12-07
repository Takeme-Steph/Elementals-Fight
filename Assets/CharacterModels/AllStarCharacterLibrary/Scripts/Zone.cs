using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Zone : MonoBehaviour 
{
	public Game 				game;
	public bool 				world;
	public int 					index;
	public List<Zone> 			zones;
	public List<envVars>		envs;
	public List<Collider>		jumpTargets;
	public List<Collider>		platforms;
	public List<Collider>		floors;
	public	Transform			player;
	public ASCLBasicController 	abc;
	public Collider 			collider;

	void Start()
	{

		collider = gameObject.GetComponent<Collider>();
		collider.enabled=false;

	}

	void Update()
	{

	}
}
