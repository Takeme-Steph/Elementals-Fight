
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#pragma warning disable

public class EnemyController : MonoBehaviour 
{

	public int				ability;
	public AbilityObject[]	abilities;

    public Vector3 		movementTargetPosition;
	//public Transform	moveTarget;
	public Vector3		lookAtPos;
	//public Transform	lookTarget;
	public float		rotateSpeed = 20.0f; //used to smooth out turning

	public Game			game;
	public Animator 	animator;
	public Stats		stats;

	public bool 		targeted=false;

	public Transform	hitReport;//this is for a text mesh object that tells us what damage we did...we need to know where this instance is so we can instantiate off of it
	public Transform	particleHit;//this is for a particle emmiter that shows us a hit...we need to know where this instance is so we can instantiate off of it

	public int 			WeaponState=0;//unarmed, 1H, 2H, bow, dual, pistol, rifle, spear and ss(sword and shield)
	public bool 		attacking=false;// we need this so we can take lock the direction we are facing during attacks, mecanim sometimes moves past the target which would flip the character around wildly
	public Vector3 		attackPos;
	public bool			leadShots;
	public float		shotTimer;
	public bool			hitCheck;

	public float		blockedTime;
	public bool			dropBlocked;
	public Vector3		lastFlat;
	public bool			wallBlocked;
	public bool			shotBlocked;
	public float		shotBlockedTime;

	public float		visualRange;
	public float		playerDistance;
	public Vector3		playerDir;
	public bool			playerSighted=false;

	public Vector3		formationDir;
	public Vector3		formationCross;

	public Transform	obstacle;
	public bool			closer;
	public bool			avoiding;
	public bool			enemyBlockage;	
	public float		dodgeTime;

	public string		overName;
	public float		hitY;
	public float		hitN;
	public bool			foundFloor;
	public Collider		floorPlane;//in this demonstration this is set manually, the Retail Ability system has methods for dealing with this automatically via data structures for environments
	public Zone			currentZone;

	Transform 			destFloor;
	TrajectoryTarget	jumpTarget;
	public bool			jumping;
	public float		jumpTime;

	float				gravity = -0.3f;//unused in this demonstration
	public bool			falling = false;
	public float		fallspeed = 0.0f;
	public float		fallForwardSpeed = 0.3f;
	
	RaycastHit hit;
	Ray ray;

	// Use this for initialization
	void Start () 
	{	
		game = FindObjectOfType(typeof(Game)) as Game;
		animator = GetComponentInChildren<Animator>();//need this...
		stats = GetComponentInChildren<Stats>();

		movementTargetPosition = transform.position;//initializing our movement target as our current position
		lookAtPos =  transform.position;
		lookAtPos.y = transform.position.y;

		stats.hitCheck=false;
		animator.SetInteger("WeaponState", WeaponState);
		animator.SetBool("Idling", true);//stop moving
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		if(!game.paused)
		{
			if(stats.health<=0)
			{
				goto Dead;
			}

			//two very important variable that are used extensively in this controller
			playerDistance = Vector3.Distance(game.player.position,transform.position);
			playerDir = (game.player.position-transform.position).normalized;

			if(stats.hitCheck)//hitCheck is a boolean variable, it gets set by mecanim attack states, if mecanim set it...then we need to do hit checks right now
			{
				
				if(abilities[0].ability.collChecks[0].checkType == CollCheckType.angleRange)
				{
					//AngleRanged
					float angle=abilities[0].ability.collChecks[0].conicAngle/2;
					Vector3 tDelta = game.player.position - transform.position;
					float tAngle = Vector3.Angle(transform.forward,tDelta);
					if (tAngle< 0) tAngle*=-1;
					if (tAngle<angle)
					{
						if(Vector3.Distance(transform.position, game.player.position)<abilities[0].ability.collChecks[0].range)
						{
							//we have a hit
							float damage1 = abilities[0].ability.collChecks[0].spawns[0].lowValue;
							float damage2 = abilities[0].ability.collChecks[0].spawns[0].highValue;
							float damage =UnityEngine.Random.Range(damage1,damage2);

							Transform tm = (Transform) Instantiate(hitReport , (game.player.position + new Vector3(0.0f,1.6f,0.0f)),Quaternion.identity);
							tm.gameObject.SetActive(true);
							Hit tmHit = tm.GetComponent<Hit>();
							tmHit.text = ((int)(Mathf.Abs(damage))).ToString();
							game.abc.stats.health-=damage;

							Transform ph = (Transform) Instantiate(particleHit , (game.player.position + new Vector3(0.0f,1.5f,0.0f)),Quaternion.identity);
							ph.transform.LookAt(Camera.main.transform.position);
							ph.transform.position += (ph.transform.forward * 2.0f);
							ph.gameObject.SetActive(true);
						}
					}
				}
				stats.hitCheck = false;
			}


			BlockedShot();//check for shot blocking

			if(!shotBlocked && playerDistance<visualRange)
			{
				if(!playerSighted)
				{
					visualRange *= 2.0f;
					for(int i=0;i<game.enemies.Length;i++)
					{
						if(Vector3.Distance(transform.position,game.enemies[i].transform.position)<visualRange)
						{
							game.enemies[i].playerSighted =true;
							game.enemies[i].visualRange *= 2.0f;
						}
					}
				}
				playerSighted =true;
			}

			if(stats.health != stats.maxHealth)
			{
				if(!playerSighted)
				{
					visualRange *= 2.0f;
					for(int i=0;i<game.enemies.Length;i++)
					{
						if(Vector3.Distance(transform.position,game.enemies[i].transform.position)<visualRange)
						{
							game.enemies[i].playerSighted =true;
							game.enemies[i].visualRange *= 2.0f;
						}
					}
				}
				playerSighted =true;
			}

			animator.SetInteger("WeaponState", WeaponState);

			//As long as we are not dead
			if(game.abc.stats.health>0.0f)
			{
				//RUNGY, add a level of if based on OKAttack (AI manuvering may not want to attack yet)
				//okAttack should be set to true by default and no code in this file should turn okAttack to TRUE
				if(!avoiding)
				{
					if(!shotBlocked)
					{
						shotBlockedTime = 0.0f;
						if(shotTimer <= 0.0f)
						{
							attacking =false;
							if(playerDistance < abilities[0].ability.collChecks[0].range)
							{
								//Rungy change this to check if the ability can be used while moving
								animator.SetBool("Idling", true);//stop moving

								animator.SetInteger("WeaponState", abilities[0].ability.weaponState);// probably would be better to check for change rather than bashing the value in like this
								shotTimer = abilities[0].ability.rateOfFire;

								movementTargetPosition = transform.position; //GOOD we are attacking so lock our position to where we are

								attackPos = game.player.position;// establish the point that we hit with the mouse
								attackPos.y = transform.position.y;//use our height for the LOOKAT function, so we stay level and dont lean the character in weird angles
								Vector3 attackDelta = attackPos - transform.position;//we need the Vector delta which is an un-normalized direction vector

								lookAtPos = transform.position + playerDir;//GOOD, we are not shot blocked, go for the player
								lookAtPos.y = transform.position.y;

								attackPos = transform.position + attackDelta.normalized * 20.0f;//look 20 meters ahead, so we don't spin around wildly if mecanim moves past the target
								animator.SetTrigger("Use");//tell mecanim to do the attack animation(trigger)

								attacking =true;
							}
						}
					}
				}
			}
			else
			{
				animator.SetBool("NonCombat", true);
				animator.SetBool("Idling", true);
			}

			if(shotTimer>0.0f)
			{
				shotTimer-=Time.deltaTime;
			}
			else
			{
				attacking=false;
			}

			/*if(wallBlocked || dropBlocked)
			{
				float closestT = 100.0f;
				int newIndex = game.abc.trailIndex;
				for(int i=0;i<game.abc.trailSize;i++)
				{
					float tempD = Vector3.Distance(transform.position,game.abc.trail[i]);
					if(tempD<closestT) 
					{
						closestT = tempD;
						newIndex = i;
					}
				}
				movementTargetPosition = game.abc.trail[newIndex];
				lookAtPos = movementTargetPosition;
			}
			*/


			//////////////////////////////
			
			//GROUND DETECTION
			
			//////////////////////////////
			ray.direction = transform.up*-1;//make a downward ray, negative "up" is "down"
			ray.origin = transform.position+transform.up;//one meter up from our feet
			bool foundOne = false;
			float closest=210.0f;
			RaycastHit target = new RaycastHit();
			for(int i=0;i<game.allZones.Length;i++)
			{
				for(int f=0 ; f<game.allZones[i].floors.Count ; f++)
				{
					//RUNGY, change from ALLZONES to zone[currentZone].zones, should only be checking zone connected data
					//If a floor is the closest...
					if (game.allZones[i].floors[f].Raycast (ray, out hit, 500)) 												
					{
						float dist = Vector3.Distance(hit.point,ray.origin);
						if(!foundOne)
						{
							//we are going to take the first floor we check so that we at least have one
							target = hit;
							floorPlane=game.allZones[i].floors[f];
							closest = dist;
							foundOne = true;
						}
						
						if(dist <= closest)
						{
							closest = dist;
							target = hit;
							floorPlane=game.allZones[i].floors[f];
							//RUNGY...need to check for PLATFORMS HERE BADLY
							//AND SETPLATFORM
						}
					}
				}
			}
			
			foundFloor = false;// reseting this for the next two raycasts
			
			//use two ground checks...one higher...the other lower
			if(floorPlane.Raycast(ray, out hit, 1.0f))
			{
				//always hit if we are going up
				if(hit.point.y>(transform.position.y + 0.02f))
				{
					transform.position=hit.point;
					foundFloor = true;//RE-using foundFloor bool, to represent "ON FLOOR" status
				}
				//angler.up=hit.normal;
			}
			else if(floorPlane.Raycast(ray, out hit, 1.2f))
			{
				if(!jumping)
				{
					// lower hit check for going down ramps
					if(hit.point.y < (transform.position.y - 0.02f))
					{
						transform.position=hit.point;
						foundFloor = true;//RE-using foundFloor bool, to represent "ON FLOOR" status
					}
					//angler.up=hit.normal;
				}
			}
			else if(!jumping && !foundFloor)//we have no ground contact, and if we're not jumping, then we are falling
			{
				//Falling
				if(falling==false) 
				{
					//animator.SetBool("Falling", true);//start falling animation
					fallForwardSpeed = 0.03f;
				}
				animator.SetBool("Idling", true);
				falling=true;
				transform.parent=null;
				fallspeed+=0.3f;
				Vector3 v = new Vector3(0.0f,fallspeed*Time.deltaTime,0.0f);
				fallForwardSpeed = fallForwardSpeed * (1.0f -(0.9f * Time.deltaTime)); //this will diminish the forward speed, but never negate it
				//v += -(transform.forward * fallForwardSpeed);
				transform.position -= v;
				transform.position += transform.forward*fallForwardSpeed;
				lookAtPos = transform.position + transform.forward*0.3f;
				movementTargetPosition = transform.position + transform.forward*0.3f;
			}

			if(foundFloor==true)
			{
				fallspeed=0.0f;
				if(falling==true)//we currently have ground contact, turn off falling if we were falling
				{
					//animator.SetBool("Falling", false);//start falling animation
					falling = false;
				}
				if(floorPlane.name.ToLower().Contains("platform"))
				{
					transform.parent=floorPlane.transform;
					//movementTarget.transform.parent=floorPlane.transform;
				}
				else
				{
					transform.parent=null;
					//movementTarget.transform.parent=null;
				}
			}
			
			//////////////////////////////
			
			//HORIZONTAL/LATERAL DETECTION
			
			//////////////////////////////
			if(!jumping && !falling)
			{
				//CHECKING FOR DROP BLOCKED , STEEP DROPS THAT BLOCK US
				//make a downward ray
				ray.direction = -transform.up;
				//on meter up from our feet
				ray.origin = transform.position + transform.up + transform.forward;
				hit = new RaycastHit();
				dropBlocked = true;//set to true before checking which will reverse the state
				if(floorPlane.Raycast(ray, out hit, 120.0f))
				{
					overName = hit.transform.name;
					//RUNGY, this is where you will be getting the point contents that identify the surface type
					hitY = hit.point.y;
					hitN = Vector3.Dot(hit.normal, transform.up);
					Debug.DrawLine(ray.origin, hit.point, Color.green);//useful for visuals in editor
					if(hit.point.y > (transform.position.y - 0.9f) && hitN>0.5f)//less than a meter downward from 1.5 meters ahead
					{
						dropBlocked = false;
						if(hitN>0.7)
						{
							lastFlat = hit.point;
						}
					}
				}

				//WALL BLOCKED CHECKS
				//make a forward ray, that's about waist high
				ray.direction = transform.forward;
				//on meter up from our feet
				ray.origin = transform.position+transform.up*0.5f;
				hit = new RaycastHit();
				wallBlocked = false;
				Debug.DrawLine(ray.origin, ray.origin + ray.direction*0.5f,Color.blue);
				for(int i=0;i<game.allZones.Length;i++)
				{
					for(int f=0 ; f<game.allZones[i].floors.Count ; f++)
					{
						//If a floor is the closest...
						if(game.allZones[i].floors[f].Raycast (ray, out hit, 0.5f)) 												
						{
							if(Vector3.Dot(hit.normal,Vector3.up)<0.5f)
							{
								wallBlocked = true;//this means environment blocked movement
								break;
							}
						}
					}
				}

				if(wallBlocked)
				{
						animator.SetBool("Idling", true);
	
				}

				if(!wallBlocked && !dropBlocked)
				{
					if(playerDistance>abilities[0].ability.collChecks[0].range)
					{
						if(playerDistance<visualRange)
						{
							if(!shotBlocked)
							{
								//check for enemy blockage
								if(dodgeTime<=0.0f)
								{
									enemyBlockage = false;
									obstacle=null;
									for(int i=0;i<game.enemies.Length;i++)
									{
										if(!enemyBlockage)
										{
											if(game.enemies[i].transform!=this.transform)
											{
												if(Vector3.Distance(game.enemies[i].transform.position,transform.position+transform.forward*0.5f)<1.0f)
												{
													if(Vector3.Dot(transform.forward,(game.enemies[i].transform.position-transform.position).normalized)>0.5f)
													{
														obstacle = game.enemies[i].transform;
														avoiding=true;
														enemyBlockage = true;
													}
												}
											}
										}
									}

									if(enemyBlockage)
									{
										dodgeTime = 0.5f;
										rotateSpeed = 10.0f;
										
										//Vector3 crossDirection = Vector3.Cross((obstacle.position-transform.position).normalized,transform.up);
										
										//to the right?
										if(Vector3.Dot(transform.right,(obstacle.position-transform.position).normalized) > 0.0f)
										{
											//change direction forward and to the left

											lookAtPos = transform.position + transform.right*5.0f;
											lookAtPos.y = transform.position.y;
										}
										else
										{
											//change direction forward and to the right
											lookAtPos = transform.position - transform.right*5.0f;
											lookAtPos.y = transform.position.y;
										}
										movementTargetPosition = lookAtPos;
										lookAtPos.y = transform.position.y;
									}
								}

								if(dodgeTime>0.0f)
								{
									dodgeTime-=Time.deltaTime;
								}
								else
								{
									rotateSpeed = 20.0f;
									movementTargetPosition = game.player.position;
									lookAtPos = game.player.position;
									lookAtPos.y = transform.position.y;
									enemyBlockage = false;
									avoiding = false;
								}
							}
						}
					}

					if(Vector3.Distance(movementTargetPosition,transform.position)<1.0f)
					{
						animator.SetBool("Idling", true);
					}
					else 
					{
						animator.SetBool("Idling", false);
					}
					if(playerDistance<1.5f)
					{
						animator.SetBool("BackUp",true);
						shotTimer=0.25f;
						movementTargetPosition = transform.position;
					}
					else
					{
						animator.SetBool("BackUp",false);
					}
				}

				if(dropBlocked)
				{
					transform.position = lastFlat;
					movementTargetPosition = lastFlat - transform.forward;
					lookAtPos = lastFlat - transform.forward;
					dropBlocked = false;
					dodgeTime = 0.5f;
					animator.SetBool("Idling",false);
				}
			}


			lookAtPos.y = transform.position.y;
			Quaternion tempRot2 = transform.rotation; 	//save current rotation
			transform.LookAt(lookAtPos);						
			Quaternion hitRot = transform.rotation;		// store the new rotation
			// now we slerp orientation
			transform.rotation = Quaternion.Slerp(tempRot2, hitRot, Time.deltaTime * rotateSpeed);
			
			Vector3 gv = new Vector3(0.0f,fallspeed*Time.deltaTime,0.0f);
			transform.position -= gv;

			Debug.DrawLine(transform.position + transform.up, lookAtPos+transform.up,Color.red);
			//lookTarget.position = lookAtPos;
			//moveTarget.position = movementTargetPosition;
		Dead:
			if(stats.health<=0)
			{
				attacking=false;
			}
		}
	}

	void BlockedShot()
	{
		shotBlocked = false;
		ray.direction = playerDir;
		ray.origin = transform.position+transform.up*1.7f;
		float dist = playerDistance + 0.5f;
		hit = new RaycastHit();
		for(int i=0;i<game.allZones.Length;i++)//loop through zones
		{
			for(int f=0 ; f<game.allZones[i].floors.Count ; f++)//loop through floors
			{
				if(game.allZones[i].floors[f].Raycast (ray, out hit, dist)) 												
				{
					shotBlocked = true;
				}
			}
		}
	}



	void OnMouseEnter() 
	{
		game.abc.targetedEnemy= this.transform;
		game.abc.range = Vector3.Distance(transform.position,game.player.position);
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

