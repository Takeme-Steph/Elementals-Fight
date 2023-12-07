using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System;

public class Game : MonoBehaviour 
{
	public bool					paused=false;
	public bool					keyHeld=false;
	public bool					keyReleased;
	public	Zone[] 				allZones;		//all the zones in this level
	public int					worldIndex;
	public	Zone				currentZone;	//current with top down camera only
												//invalid if mounted or using 3rd person close

	public Collider				currentFloor;
	public	Transform			player;
	public ASCLBasicController	abc;
	public EnemyController[]	enemies;//this array is filled during START by searching for prefabs that have the enemy script attached to them

	public Vector3				formationDir;
	public Vector3				formationCross;
	public Vector3				eBary;

	public EnvTarget[]			envTargets;
	public MouseOrbit			mouseCam;
	public bool					settingAngle;


	void Start()
	{
		allZones = FindObjectsOfType(typeof(Zone)) as Zone[];
		enemies = FindObjectsOfType(typeof(EnemyController)) as EnemyController[];
		envTargets = FindObjectsOfType(typeof(EnvTarget)) as EnvTarget[];
		abc = Transform.FindObjectOfType(typeof(ASCLBasicController)) as ASCLBasicController;
		player = abc.transform;
		mouseCam = Camera.main.GetComponentInChildren<MouseOrbit>();

		//we're going to loop through all the zones to find out where we are

		bool foundFloor = false;///declaring it here because we use it elsewhere as well
		float closest=210.0f; //same for this
		RaycastHit hit = new RaycastHit();
		Ray ray = new Ray();
		ray.origin = player.transform.position;
		ray.direction = -player.transform.up;
		for(int i=0;i<allZones.Length;i++)
		{
			for(int f=0 ; f<allZones[i].floors.Count ; f++)
			{
				//If a floor is the closest...
				if (allZones[i].floors[f].Raycast (ray, out hit, 500)) 												
				{
					float dist = Vector3.Distance(hit.point,ray.origin);
					if(!foundFloor)
					{
						closest = dist;
						foundFloor = true;
						abc.floorPlane = allZones[i].floors[f];
						abc.currentZone = allZones[i];
					}
					
					if(dist <= closest)
					{
						closest = dist;
						abc.floorPlane = allZones[i].floors[f];
						abc.currentZone = allZones[i];
					}
				}
			}
		}

		for(int e=0;e<enemies.Length;e++)
		{
			foundFloor = false;///declaring it here because we use it elsewhere as well
			closest=210.0f; //same for this
			hit = new RaycastHit();
			ray = new Ray();
			ray.origin = enemies[e].transform.position;
			ray.direction = -enemies[e].transform.up;
			for(int i=0;i<allZones.Length;i++)
			{
				for(int f=0 ; f<allZones[i].floors.Count ; f++)
				{
					//If a floor is the closest...
					if (allZones[i].floors[f].Raycast (ray, out hit, 500)) 												
					{
						float dist = Vector3.Distance(hit.point,ray.origin);
						if(!foundFloor)
						{
							closest = dist;
							foundFloor = true;
							enemies[e].floorPlane = allZones[i].floors[f];
							enemies[e].currentZone = allZones[i];
						}
						
						if(dist <= closest)
						{
							closest = dist;
							enemies[e].floorPlane = allZones[i].floors[f];
							enemies[e].currentZone = allZones[i];
						}
					}
				}
			}
		}

		foreach(Zone zone in allZones)//for some wacko reason you cannot place this in the below loop, fails to init in time
		{
			zone.collider.enabled=true;
		}
		foreach(Zone zone in allZones)
		{
			foreach(EnvTarget target in envTargets)
			{
				if(zone.collider.bounds.Contains(target.transform.position))
				{
					target.zone = zone;
				}
			}
		}
		foreach(Zone zone in allZones)//for some wacko reason you cannot place this in the above loop, fails to init in time
		{
			zone.collider.enabled=false;
		}

	}

	//UPDATE
	void LateUpdate ()
	{
		settingAngle = false;
		if(!abc.useThird && !abc.mounted)
		{
			currentZone.collider.enabled = true;
			if(!currentZone.collider.bounds.Contains(player.position) || currentZone.world)
			{
				currentZone.collider.enabled = false;
				bool zoneFound=false;
				foreach(Zone zone in allZones)//for some wacko reason you cannot place this in the below loop, fails to init in time
				{
					zone.collider.enabled=true;
				}
				foreach(Zone zone in allZones)
				{
					if(zone.collider.bounds.Contains(player.position))
					{
						if(!zone.world)
						{
							if(mouseCam.useCamAngle == true)
							{
								envVars tempvars = zone.GetComponentInChildren<envVars>();
								mouseCam.camObject = tempvars.camObject;
								mouseCam.Start();
								currentZone=zone;
								zoneFound =true;
								settingAngle = true;
							}
						}
					}
				}
				if(!zoneFound)
				{
					if(mouseCam.useCamAngle == true)
					{
						envVars tempvars = allZones[worldIndex].GetComponentInChildren<envVars>();
						mouseCam.camObject = tempvars.camObject;
						mouseCam.Start();
						currentZone=allZones[worldIndex];
						zoneFound =true;
						settingAngle = true;
					}
				}
				foreach(Zone zone in allZones)//for some wacko reason you cannot place this in the above loop, fails to init in time
				{
					zone.collider.enabled=false;
				}
			}
			currentZone.collider.enabled = false;
		}
		int eCount=0;
		eBary = Vector3.zero;
		for(int i=0;i<enemies.Length;i++)
		{
			if(enemies[i].playerSighted)
			{
				if(enemies[i].playerDistance<enemies[i].visualRange);
				{
					eBary+=enemies[i].transform.position;
					eCount+=1;
				}
				eBary=eBary/eCount;
			}
		}
	}

	public bool CheckFloors(Vector3 pos,out RaycastHit hit)
	{
		Zone newZone = currentZone;
		Collider newFloor = currentFloor;
		Vector3 rayStart = pos;
		rayStart.y += 1.0f;
		Ray ray = new Ray(rayStart,new Vector3(0.0f,-1.0f,0.0f));
		bool foundFloor = false;
		RaycastHit target = new RaycastHit();
		float closest = 1000.0f;
		
		foreach(Zone zone in currentZone.zones)
		{
			foreach(Collider col in zone.floors)
			{
				if (col.Raycast (ray, out hit, 100.0f))
				{
					float dist = Vector3.Distance(hit.point,rayStart);
					if(!foundFloor)
					{
						closest = dist;
						foundFloor = true;
					}
				
					if(dist <= closest)
					{
						closest = dist;
						target = hit;
						newZone = zone;
						newFloor = col;
					}
				}
			}
			//Rungy this is where you do your platform code
			//same as check floors, but there are some very 
			//special things that get affected if we are on one
		}
		hit = target;
		if(currentFloor != newFloor)
		{
			currentZone = newZone;
			currentFloor = newFloor;
		}
		return foundFloor;
	}
	
	public bool GetDir(Ray ray, Vector3 pos,out RaycastHit hit)
	{
		bool foundFloor = false;
		RaycastHit target = new RaycastHit();
		float closest = 1000.0f;
		
		foreach(Zone zone in currentZone.zones)
		{
			foreach(Collider col in zone.floors)
			{
				if (col.Raycast (ray, out hit, 500.0f))
				{
					float dist = Vector3.Distance(hit.point,Camera.main.transform.position);
					if(!foundFloor)
					{
						closest = dist;
						foundFloor = true;
					}
				
					if(dist <= closest)
					{
						closest = dist;
						target = hit;
					}
				}
			}
		}
		hit = target;
		return foundFloor;

	}

	void OnGUI()
	{
		if(Input.GetKey(KeyCode.P))
		{
			if(!keyHeld)
			{
				keyHeld=true;
			}
		}
		else
		{
			keyHeld = false;
		}

	}

	bool FindPoint()
	{
		//succcess = false
		//determine my type
		
		//if melee
		//loop through melee points
		//bool contained = fasle
		//set bounds position(point+enemy.position)
		//check bounds contains for this each enemy
		//if contains continue(bail on this point)
		//if !contained
		//	set movementTarget
		//	return = true
		
		//else//(we're ranged)
		//loop through ranged points
		//bool contained = fasle
		//set bounds position(point+enemy.position)
		//check bounds contains for this each enemy
		//if contains continue(bail on this point)
		//if !contained
		//	set movementTarget
		//	return = true
		
		return false;
	}
}
