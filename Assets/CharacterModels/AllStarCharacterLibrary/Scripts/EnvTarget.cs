using UnityEngine;
using System.Collections;

public class EnvTarget : MonoBehaviour 
{
	public bool 			targeted=false;
	public Game 			game;
	public envTargetType	type;
	public Zone				zone;
	public Collider			collider;


	// Use this for initialization
	void Start () 
	{
		game = FindObjectOfType(typeof(Game)) as Game;
		collider = GetComponentInChildren<Collider>();
	}
	
	// Update is called once per frame
	void Update () 
	{
		
	}
	void OnMouseEnter() 
	{
		game.abc.targetedEnemy = this.transform;
		targeted = true;
	}
	
	void OnMouseExit() 
	{
		if(game.abc.targetedEnemy == this.transform)
		{
			game.abc.targetedEnemy = null;
		}
		targeted = false;
	}
	
}

public enum envTargetType
{
	targetDummy,
	button,
	destructable,
	lever,
	console,
	pickup,
	draggable
}

