using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#pragma warning disable

public class PickUp : MonoBehaviour 
{
	public Game				game;
	public bool				forEnemy;
	public bool				forPlayer;
	public float			radius;
	public StatMod[]		statmod;
	public float			amount;
	public SideAffects[]	sideaffect;
	Ray 					ray;
	RaycastHit 				hit;

	// Use this for initialization
	void Start () 
	{
		game = FindObjectOfType(typeof(Game)) as Game;
		if(forEnemy)
		{

		}
		if(forPlayer)
		{

		}
	}
	
	// Update is called once per frame
	void Update () 
	{
		if(forEnemy)
		{
			
		}
		if(forPlayer)
		{
			Vector3 delta = ray.direction = (game.player.position - transform.position);
			ray.origin = transform.position+Vector3.up;
			hit = new RaycastHit();
			if(game.player.GetComponent<Collider>().Raycast(ray, out hit, radius))
			{
				game.abc.stats.health+=amount;
				DestroyImmediate(this.transform.gameObject);
			}
		}
	}
}
