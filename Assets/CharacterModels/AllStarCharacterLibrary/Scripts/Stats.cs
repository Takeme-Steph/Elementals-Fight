using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class Stats : MonoBehaviour 
{
	public bool						isPlayer;
	Animator 						animator;
	public bool						hitCheck;
	public float					maxHealth;
	public float					health;
	float							currentHealth;
	public MeshRenderer				healthBar;
	public Texture2D[]				healthTextures;
	public Material					healthMat;
	public float					maxEnergy;
	public float					energy;
	public string					teamName;
	public TeamType[]				allyTypes;
	public TeamType[]				enemieTypes;
	public Stats					nearestEnemy;

	public Transform[]				weaponTrails;
	public bool						knockedBack;
	public bool						attacking;


	/*
	 * 		moveTarget
	 * 		moveSpeed
	 * 		radius
	 * 		lookTarget
	 * 		manuevering
	 * 		pathing
	 * 		gravity
	 * 		lastPosition
	 * 		velocity
	 * 		visualRange
	 * 		attackTarget
	 *		attacking
	 *		attackSpeed
	 *		targetedEnemy
	 *		hitCheck
	 *		shotTimer
	 * 		jumping
	 * 		jumpTarget
	 * 		jumpTime
	 * 		falling
	 * 		flying
	 * 		swimming
	 * 		dead
	 * 		currentZone
	 * 		floorPlane
	 *  	MissileOwner			missileOwner...change this to faction[], so we can have multiple factions
	 * 		bool pain
	 * 		totally revamp ability system to be spawn based
	 * 		
	 *  	
	*/


	// Use this for initialization
	void Start ()
	{
		animator = GetComponentInChildren<Animator>();
		maxHealth=health;
		currentHealth = health;
		healthMat = healthBar.material;
		hitCheck=false;
	}
	
	// Update is called once per frame
	void Update () 
	{
		//////////////////////////////////
		
		// HEALTH
		
		//////////////////////////////////
		if(health>maxHealth)
		{
			health=maxHealth;
			currentHealth = maxHealth;
			healthMat.SetTexture("_MainTex",healthTextures[0]);
			healthMat.mainTextureOffset = new Vector2((1-((1.0f/maxHealth)*(maxHealth-health))*0.5f),0.0f);
		}

		if (health!=currentHealth)
		{
			animator.SetTrigger("Pain");
			if(health<=0)
			{
				int tempInt =(int)Mathf.Abs(UnityEngine.Random.Range(1.0f,3.9f));

				animator.SetInteger("Death",tempInt);
				this.enabled=false;
				Collider collider = GetComponentInChildren<Collider>();
				collider.enabled =false;
				healthBar.enabled=false;
			}
			else if(health < (maxHealth*0.35f))
			{
				healthMat.SetTexture("_MainTex",healthTextures[2]);
			}
			else if(health < (maxHealth*0.65f))
			{
				healthMat.SetTexture("_MainTex",healthTextures[1]);
			}
			else
			{
				healthMat.SetTexture("_MainTex",healthTextures[0]);
			}
			if(health>0)
			{
				healthMat.mainTextureOffset = new Vector2((1-((1.0f/maxHealth)*(maxHealth-health))*0.5f),0.0f);
			}
			currentHealth = health;
		}
	}
}


// ABILITY ->collchecks->spawns
[Serializable]
public class ASAbility 
{
	public string		name;
	public string		desc;
	public Texture2D	smallerGameBarIcon;
	public Texture2D	abilityMenuIcon;
	public CostType		cost;
	public float		costValue;
	public bool 		interuptable;
	public bool 		useMoving;
	public bool			continuous;
	public float 		rateOfFire;		//seconds per shot, not the other way around
	public float		coolDown;
	public int			weaponState; 	//might need to switch weapon TYPES for this sequence step(for example...from rifle to pistol)
	public int			weaponIndex; 	//might have several different pistols(for example)
	public int			comboAttackCount;
	public int			animIndex;
	[SerializeField]
	public ASCollCheck[]	collChecks;
}

//each sequence can have one or more collsions
// collision checkType "none" means don't check for collision
// just do Spawn(s) which are NOT JUST for spawning by the way
[Serializable] //all abilities have collision checks, even if they are NULL
public class ASCollCheck //logic...ability->COLLCHECKS->spawns
{
	public CollCheckType		checkType;	//none = no coll check...spawn automatically
	public Collider				collider;
	public Vector3 				position;
	public Vector3 				direction;
	public float 				conicAngle;
	public float 				range;
	[SerializeField]
	public ASSpawn[]			spawns;
}

//SPAWNS are used for all buffs, damage and of course spawning other entities
[Serializable]		
public class ASSpawn	//logic...ability->collchecks->SPAWNS
{
	public string 				name;
	public Transform 			prefab;
	public ParentType			parent;
	public float				speed;
	public float 				lowValue; 		//for value based stat modification
	public float 				highValue; 		//if zero then it's a straight value taken from lowest
}

public enum CollCheckType
{
	None,
	angleRange,
	beam,
	missile,
	toss,
	distance,
	worldPoint
}

public enum FrequencyType
{
	oneShot,
	repeating,
	constant
}

public enum ParentType
{
	None,
	Caster,
	Self,
	Target,
	FirstSpawn,
}
public enum StatMod
{
	None,
	Health,
	MoveSpeed,
	Defense,
	AttackSpeed,
	Strength,
}

public enum CostType
{
	None,
	health,
	energy,
	time,
	money
}

public enum SideAffects
{
	None,
	Bleeding,
	Poisoned,
	Frozen,
	Slowed,
	Petrified,
	Stunned,
	KnockedDown,
	KnockBack
}

public enum TeamType
{
	None,
	Player,
	Enemy,
	Hero,
	Villain,
	Good,
	Evil,
	Monster,
	Beast,
	Team01,
	Team02,
	Team03,
	Team04,
	Team05,
}