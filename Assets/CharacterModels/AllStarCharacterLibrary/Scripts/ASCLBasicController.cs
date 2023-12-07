using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
#pragma warning disable

public class ASCLBasicController : MonoBehaviour 
{

	public AbilityObject[]	abilities;
	public int[]			abilityKeys;
	public int				abilityIndex;
	public bool				abilityUse=false;
	int						ahc=1; //ability hit counter, combo attacks have more than one hit, this variable keeps track of how many hits we have used in update

    public Game			game;
	public Stats		stats;
	
	//Mecanim parameters, these get fed to mecanim at the end of LateUpdate

	//because they take .006 seconds per parameter to set

	//as opposed to .0000003 seconds to set as c# variables
	public Animator 	animator;
	
	public int 			WeaponState = 0;		//unarmed, 1H, 2H, bow, dual, pistol, rifle, spear and ss(sword and shield)
	bool				idling;
	public float		moveType = 0.0f;	//0=forward,1=right,2=back,3=left,4 forward(for blending with 3Left)
	bool				pain;
	int					painType;			//this is currently unused
	int					death;				//0=not dead
	bool				jump;				//mecanim has trigger not bool
	public bool			jumping;
	public float		jumpTime;
	public bool			falling = false;

	bool				use;
	public float		aimX;
	public float		aimY;
	bool				nonCombat;

	public Zone			currentZone;
	Zone 				newZone; //used if we change zones
	public Collider		floorPlane;
	public Collider 	destFloor; //jumping or clicking this floor...it's not always the same as the one we are standing on...(which is "floorPlane")
	public Collider		clickedFloor;
	bool 				foundFloor;
	public Collider		attackPlane;

	public Transform	hitReport;//this is for a text mesh object that tells us what damage we did...we need to know where this instance is so we can instantiate off of it
	public Transform	particleHit;//this is for a particle emmiter that shows us a hit...we need to know where this instance is so we can instantiate off of it



	public bool 		hitCheck = false;//<<< IMPORTANT mecanim tells us to perform a hit check at a specific point in attack animations by settting this to TRUE
	public bool			rightButtonDown=false;//we use this to "skip out" of consecutive right mouse down input...

	public bool 		wasAttacking=false;// we need this so we can take lock the direction we are facing during attacks, mecanim sometimes moves past the target which would flip the character around wildly
	public Vector3 		attackPos;
	public Transform	targetedEnemy;
	public Transform	aimAssist;
	public float		range;
	Vector3 			attackDelta;

	public float			aimTime;
	public Renderer			aimPoint;
	public bool				useThird;
	public CamAngleObject[]	aimObjs;
	public CamAngle[]		aimAngles;

	public bool			mounted;
	public Collider		mountedPlane;
	public Vector3		lookAtPos;
	public Renderer		lookAtTarget;
	public Renderer		movementTarget;
	public Renderer		attackTarget;
	public float		rotateSpeed = 20.0f; //used to smooth out turning
	public Transform	angler;
	TrajectoryTarget	jumpTarget;
	float				gravity = -0.3f;//unused in this demonstration
	public float		fallspeed = 0.0f;
	public float		fallForwardSpeed = 0.3f;
	public float		velocityPS;
	public Vector3		lastPosition;
	public Vector3		lastDir; //we may be moving sideways or backwards

	RaycastHit 			hit;// RayCastHits hold very useful info such as hitnormal and location
	Ray 				ray;
	public bool			keyHeld=false;
	public bool			keyReleased=true;

	string tempString = "LMB=move RMB=attack P=powers SPACE=jump, SPACE whileJumping=swing";
	

	// Use this for initialization
	void Start () 
	{	
		animator = GetComponentInChildren<Animator>();//get the mecanim controller
		animator.SetInteger("WeaponState",WeaponState);
		stats = GetComponentInChildren<Stats>();//get the mecanim controller
		jumpTarget = GetComponentInChildren<TrajectoryTarget>();
		game = FindObjectOfType(typeof(Game)) as Game;

		movementTarget.transform.position = transform.position;//initializing our movement target as our current position
		movementTarget.enabled = true;
		lookAtPos = transform.position+(transform.forward*0.2f);
		wasAttacking=false;
		abilityUse=false;
		//trail = new Vector3[trailSize];
		//trailMarker = new Transform[trailSize];
		//for(int i=0;i <trailSize;i++)
		//{
		//	trail[i]=transform.position;
		//	Transform tm = (Transform) Instantiate(trailPylon , trail[i], trailPylon.rotation);
		//	trailMarker[i] = tm;
		//}
		//trailIndex=0;
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		if(!game.paused)
		{
			//The Update logic does:
			//
			//	Checks whether or not mecanim told us to check for a hit
			//		Mecanim is great for sending us this info because it can play animations at any speed, rather than calculate the time
			//		we use it to tell us the appropriate hit time
			//
			//	Get UI input from keyboard, and mouse clicks
			//
			//	Tells mecanim what animation we should be playing based on variables such as idling, pain or death
			//
			//	Handle movement and direction


			////////////////////////////////

			/// ABILITY SYSTEM

			////////////////////////////////
			if(stats.hitCheck)//hitCheck is a boolean variable, it gets set by mecanim attack states, if mecanim set it...then we need to do hit checks right now
			{
				wasAttacking =true;
				//RUNGY
				//code for chained attacks should go here
				//animator.SetInteger("AttackIndex", abilities[abilityIndex].ability.animIndex);


				///RUNGY you have to setup for chained attacks HERE
				for(int collisionIndex=0; collisionIndex<abilities[abilityIndex].ability.collChecks.Length; collisionIndex++)
				{
					if(ahc<1) ahc=1;//we may have a "double pulse" coming from Mecanim so...
					ASCollCheck abilColl = new ASCollCheck();
					abilColl = abilities[abilityIndex].ability.collChecks[collisionIndex];
					if(abilColl.checkType == CollCheckType.angleRange)
					{
						//ANGLE RANGE which can be used for any angle/range including radial attacks
						for(int i =0;i<game.enemies.Length;i++)//loop throught the enemies
						{
							CheckForHit(game.enemies[i], abilColl);
						}
						ahc-=1;// some abilities have multiple checks, so when we use an ability, we set ahc to the number of hits in the ability (combo punches for example)
					}
					
					if(abilColl.checkType == CollCheckType.worldPoint)
					{
						Transform tm = (Transform) Instantiate(abilColl.spawns[0].prefab);
						Vector3 tempPos = tm.position;
						Quaternion tempRot = tm.rotation;
						tm.parent = transform;
						tm.localPosition = tempPos;
						tm.localRotation = tempRot;

						if(abilColl.spawns[0].parent == ParentType.Self)
						{
							tm.parent=transform;
						}
					}
				}
				stats.hitCheck = false;// we are done checking, reset the hitCheck bool
				//targetedEnemy = null;
			}

			if(!useThird && !mounted)
			{
				//if the mouse pointer is over an enemy, then set aim vectors to do aim blending and look at that enemy
				//but only if we are "somewhat" facing that enemy
				float aimX2 = 0.998f;
				float aimY2 = 0.0f;
				if(targetedEnemy)
				{
					Vector3 tempDelta = (targetedEnemy.position - transform.position).normalized;
					aimY2 = tempDelta.y;
					Vector3 flatdelta = new Vector3(tempDelta.x ,0.0f ,tempDelta.z);
					aimX2 = Vector3.Distance(Vector3.zero,flatdelta);
					flatdelta = flatdelta.normalized;
					if(Vector3.Dot(transform.forward,flatdelta)<0.9f)//we need to be "somewhat" facing the target direction
					{
						aimX2 = 0.998f;
						aimY2 = 0.0f;
					}
				}
				aimX = Mathf.Lerp(aimX,aimX2,Time.deltaTime*20.0f);
				aimY = Mathf.Lerp(aimY,aimY2,Time.deltaTime*20.0f);
				animator.SetFloat("AimY", aimY);
				animator.SetFloat("AimX", aimX);
			}
			else if(useThird)
			{
				animator.SetFloat("AimY", aimY);
				animator.SetFloat("AimX", aimX);
			}

			//////////////////////
			//
			//	ATTACKS UI
			//
			//////////////////////
			/// 
			////Right MOUSE BUTTON
			/// 
			//////////////////////
			if( ! Input.GetKey(KeyCode.LeftAlt)) // if we're changing camera transforms, do not use "USE"
			{
				if(Input.GetMouseButton(1))// are we using the right button?
				{
					if(rightButtonDown != true)// was it previously down? if so we are already using "USE" bailout (we don't want to repeat attacks 800 times a second...just once per press please
					{
						abilityUse=true;
						abilityIndex=abilityKeys[0];
					}
				}
				else
				{
					rightButtonDown=false;
				}
			}
			///////////////////
			/// 
			///		KEYBOARD
			///
			///////////////////

			if(Input.GetKey(KeyCode.A))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[1];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.S))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[2];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.D))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[3];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.F))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[4];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.G) )
			{
			   if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[5];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.H))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[6];
					keyHeld=true;
				}
			}
			else if(Input.GetKey(KeyCode.J))
			{
				if(!keyHeld)
				{
					abilityUse=true;
					abilityIndex=abilityKeys[7];
					keyHeld=true;
				}
			}
			else
			{
				keyHeld=false;
			}

			if(abilityUse && abilities[abilityIndex].ability.cost==CostType.energy)
			{
				if(stats.energy>abilities[abilityIndex].ability.costValue)
				{
					stats.energy-=abilities[abilityIndex].ability.costValue;
				}
				else
				{
					abilityUse=false;
				}
			}


			animator.SetInteger("WeaponState", abilities[abilityIndex].ability.weaponState);
			animator.SetInteger("Ability", abilityIndex);



			//Abilities that just spawn are check type "none" and get processed after targeting code
			if(abilityUse && abilities[abilityIndex].ability.collChecks[0].checkType!=CollCheckType.None)
			{
				wasAttacking =true;
				if(targetedEnemy)
				{
					EnemyController ec = targetedEnemy.GetComponentInChildren<EnemyController>();
					if(ec)
					{
						if(ec.stats.health <= 0.0f)
						{
							targetedEnemy = null;
						}
					}
				}

				if(!mounted && !useThird)
				{
					Vector3 oldAP =attackPlane.transform.position;
					Quaternion oldARot = attackPlane.transform.rotation;

					if(targetedEnemy)
					{
						//move the attack plane to the enemies position + 1 meter high
						attackPlane.transform.position=targetedEnemy.position+(transform.up);
						Vector3 tempV3 = Camera.main.transform.position;
						tempV3.y=targetedEnemy.position.y; //attackplane should only face camera direction not tilt at all
						attackPlane.transform.up = Camera.main.transform.up;
						attackPlane.transform.forward = Camera.main.transform.forward;
					}
					
					attackPlane.enabled=true;

					ray = Camera.main.ScreenPointToRay (Input.mousePosition);//get a ray that goes from the camera -> "THROUGH" the mouse pointer - > and out into the scene
					if(attackPlane.Raycast(ray, out hit, 500.0f)) 												
					{
						attackPos = hit.point;// establish the point that we hit with the mouse
						attackPos.y = transform.position.y;//use our height for the LOOKAT function, so we stay level and dont lean the character in weird angles
						//check if we are at idle
						//check ability for useWhenRunning
						//determine if we should run backwards,forwards or sideways
						//if we are running
						//and if we attack to the sides or behind us
						//use appropriate run animation and fire whilst running
						//set attackTime counter so we can exit aiming while running
						//AND look forward again
						if(velocityPS<0.1)
						{
							lookAtPos = transform.position + (attackPos - transform.position).normalized * 0.3f;//lookat the target using this vector
							movementTarget.transform.position = lookAtPos;
						}
						attackDelta = hit.point;//we need the Vector delta which is an un-normalized direction vector
						animator.SetTrigger("Use");//tell mecanim to do the attack animation(trigger)
						ahc = abilities[abilityIndex].ability.collChecks.Length;//ahc=ability hit counter, used for animations that have multiple hits like combos
						//animator.SetBool("Idling", true);//stop moving
						rightButtonDown = true;//right button was not down before, mark it as down so we don't attack 800 frames a second 
						wasAttacking =true;//some mecanims will actually move us past the target, so we want to keep looking in one direction instead of spinning wildly around the target
					}

					attackPlane.enabled=false;
					attackPlane.transform.position = oldAP;
					attackPlane.transform.rotation = oldARot;
				}
				else if(mounted)
				{
					ray.origin = game.mouseCam.transform.position;
					ray.direction = (aimPoint.transform.position - ray.origin).normalized;
					mountedPlane.enabled=true;
					if(mountedPlane.Raycast(ray, out hit, 500.0f))
					{
						attackPos = hit.point;// establish the point that we hit with the mouse
						attackPos.y = transform.position.y;//use our height for the LOOKAT function, so we stay level and dont lean the character in weird angles
						attackDelta = hit.point;//we need the Vector delta which is an un-normalized direction vector
						animator.SetTrigger("Use");//tell mecanim to do the attack animation(trigger)
						ahc = abilities[abilityIndex].ability.collChecks.Length;//ahc=ability hit counter, used for animations that have multiple hits like combos
						animator.SetBool("Idling", true);//stop moving
						rightButtonDown = true;//right button was not down before, mark it as down so we don't attack 800 frames a second 
						wasAttacking =true;//some mecanims will actually move us past the target, so we want to keep looking in one direction instead of spinning wildly around the target
					}
					mountedPlane.enabled=false;
				}
				else if(useThird)
				{
					targetedEnemy = null;
					ray.origin = game.mouseCam.transform.position;
					ray.direction = game.mouseCam.transform.forward;
					attackPos = (ray.origin + ray.direction*1.0f);
					//lookAtPos = (transform.position + transform.forward).normalized * 0.3f;//lookat the target using this vector
					//movementTarget.transform.position = lookAtPos; 
					attackDelta = attackPos;//we need the Vector delta which is an un-normalized direction vector
					animator.SetTrigger("Use");//tell mecanim to do the attack animation(trigger)
					ahc = abilities[abilityIndex].ability.collChecks.Length;//ahc=ability hit counter, used for animations that have multiple hits like combos
					//animator.SetBool("Idling", true);//stop moving
					rightButtonDown = true;//right button was not down before, mark it as down so we don't attack 800 frames a second 
					wasAttacking =true;//some mecanims will actually move us past the target, so we want to keep looking in one direction instead of spinning wildly around the target
				}

				// AIM ASSIST
				// DOT each enemy with the attack vector
				// the one with the highest dot value will be our vector
				// only values above .85 should be considered
				// attackDelta IS a hit point for missiles
				// attackPos IS locked to player's y(height)for beams
				float closestOne=-2.0f;
				Vector3 clickVector = (attackPos-transform.position).normalized;
				if(useThird || mounted)
				{
					clickVector = (aimPoint.transform.position - game.mouseCam.transform.position).normalized;
				}
				Vector3 eVector3;

				if(!targetedEnemy)
				{
					for(int i=0;i<game.enemies.Length;i++)
					{
						if(game.enemies[i].stats.health>0.0f)
						{
							eVector3 = (game.enemies[i].transform.position-transform.position).normalized;
							if(Vector3.Dot(clickVector,eVector3)>closestOne)
							{
								closestOne = Vector3.Dot(clickVector,eVector3);
								if(Vector3.Dot(clickVector,eVector3)>0.85f)
								{
									//we use a secondary targeted type known as aimAssist
									//we do this because forcing data into targetedEnemy would screw mouse over behavior badly
									aimAssist = game.enemies[i].transform;
									attackTarget.transform.position = aimAssist.position;
								}
							}
						}
					}
				}
				if(targetedEnemy)
				{
					attackTarget.transform.position = targetedEnemy.position;
				}
				else
				{
					attackTarget.transform.position = attackPos;
				}
				abilityUse=false;
			}
			else
			{
				//the check type = none...process the ability which in all likeliness is just a spawner
				if(abilityUse && abilities[abilityIndex].ability.collChecks[0].checkType==CollCheckType.None)
				{
					animator.SetTrigger("Use");
					wasAttacking =true;
				}

				abilityUse=false;
			}

			//ATTACKS reset right mouse button
			if (Input.GetMouseButtonUp(1))//ok, we can clear the right mouse button and use it for the next attack
			{
				if (rightButtonDown == true)
				{
					rightButtonDown = false;
				}
			}


			if(!mounted)
			{

				/// JUMPING start
				foundFloor = false;///declaring it here because we use it elsewhere as well

				//jump key pressed
				if(Input.GetKey(KeyCode.Space)&& !jumping && !falling)
				{
					//RUNGY ADD THIS IN loop througbh the ability bar, do we have a jump?
					Jump ();
				}





				if(jumping)
				{
					lookAtPos.y = transform.position.y;
					if(Vector3.Distance(movementTarget.transform.position,transform.position)<0.5f)
					{
						//movementTarget.transform.position = transform.position;
						lookAtPos = transform.position + (transform.forward*0.2f);
					}
				}

				if(!jumping)
				{
					if(!wasAttacking)
					{
						if(Vector3.Distance(lookAtPos,transform.position)>0.5f)
						{
							animator.SetBool("Idling", false);
							animator.SetFloat("MoveType",moveType);
						}
						else
						{
							animator.SetBool("Idling", true);
							movementTarget.enabled = false;
							lookAtPos = transform.position + (transform.forward*0.2f);
						}
					}
					else
					{//this level of else means wasAttacking is true
						//as long aas the ability is not a use moving type, we can look at our target
						if(!abilities[abilityIndex].ability.useMoving)
						{
							lookAtPos = attackPos;
							lookAtPos.y=transform.position.y;
							movementTarget.transform.position = lookAtPos;
							if(Vector3.Distance(attackPos,transform.position)<0.5f)
							{
								animator.SetBool("Idling", true);
								movementTarget.enabled = false;
								movementTarget.transform.position=transform.position;
								lookAtPos=transform.position+transform.forward;
							}
						}
					}
				}




				//////////////////////////////

				//GROUND DETECTION

				//////////////////////////////
				

				ray.direction = transform.up*-1;//make a downward ray, negative "up" is "down"
				ray.origin = transform.position+transform.up;//one meter up from our feet
				foundFloor = false;
				float closest=210.0f; //same for this
				RaycastHit target = new RaycastHit();

				newZone = null;
				for(int i=0;i<currentZone.zones.Count;i++)//loop through current zones and its connected zones
				{
					for(int f=0 ; f<currentZone.zones[i].floors.Count ; f++)//loop throught the floors of the zones
					{
						//If a floor is the closest...
						if (currentZone.zones[i].floors[f].Raycast (ray, out hit, 500)) 												
						{
							float dist = Vector3.Distance(hit.point,ray.origin);
							if(!foundFloor)
							{
								//we are going to take the first floor we check so that we at least have one
								target = hit;
								floorPlane= currentZone.zones[i].floors[f];
								newZone = currentZone.zones[i];
								closest = dist;
								foundFloor = true;
							}
							
							if(dist <= closest)
							{
								closest = dist;
								target = hit;
								floorPlane=currentZone.zones[i].floors[f];
								newZone = currentZone.zones[i];
								//RUNGY...need to check for PLATFORMS HERE BADLY
								//AND SETPLATFOR
							}
						}
					}
				}

				if(newZone!=null)
				{
					currentZone = newZone;
				}

				foundFloor = false;// reseting this for the next two raycasts

				//use two ground checks...one higher...the other lower
				if(floorPlane.Raycast(ray, out hit, 1.0f))
				{
					//always hit if we are going up
					if(hit.point.y>(transform.position.y + 0.02f))
					{
						transform.position=hit.point;
						lookAtPos.y = transform.position.y;
						foundFloor = true;//RE-using foundFloor bool, to represent "ON FLOOR" status
					}
					angler.up=hit.normal;
				}
				else if(floorPlane.Raycast(ray, out hit, 1.2f))
				{
					if(!jumping)
					{
						// lower hit check for going down ramps
						if(hit.point.y < (transform.position.y - 0.02f))
						{
							transform.position=hit.point;
							lookAtPos.y = transform.position.y;
							foundFloor = true;//RE-using foundFloor bool, to represent "ON FLOOR" status
						}
						angler.up=hit.normal;
					}
				}
				else if(!jumping && !foundFloor)//we have no ground contact, and if we're not jumping, then we are falling
				{
					//Falling
					if(falling==false) 
					{
						animator.SetBool("Falling", true);//start falling animation
						fallForwardSpeed = 0.15f;
						lookAtPos = movementTarget.transform.position + (transform.forward);
					}
					falling=true;


					transform.parent=null;
					movementTarget.transform.parent=null;

					fallspeed+=(Time.unscaledDeltaTime/0.015f)*0.3f;

					Vector3 v = new Vector3(0.0f,fallspeed*Time.deltaTime,0.0f);
					fallForwardSpeed = fallForwardSpeed * (1.0f -(0.9f * Time.deltaTime)); //this will diminish the forward speed, but never negate it
					//v += -(transform.forward * fallForwardSpeed);
					transform.position -= v;
					transform.position += transform.forward*(fallForwardSpeed*(Time.unscaledDeltaTime/0.015f));

				}

				//unconditional velocity check for falling through the world
				ray.origin = lastPosition + transform.up;
				velocityPS = Vector3.Distance(transform.position,lastPosition)/Time.unscaledDeltaTime;
				ray.direction = -transform.up;//velocity.normalized;
				float speed = Vector3.Distance(transform.position,lastPosition);
				if(floorPlane.Raycast(ray, out hit, speed))
				{
					if(hit.point.y>transform.position.y)
					{
						transform.position = hit.point;
						foundFloor=true;
					}
				}


				if(foundFloor)
				{
					fallspeed=0.0f;
					if(falling==true)//we currently have ground contact, turn off falling if we were falling
					{
						animator.SetBool("Falling", false);//start falling animation
						falling = false;
						movementTarget.transform.position= transform.position + (transform.forward*0.2f);
						lookAtPos = transform.position + (transform.forward*0.2f);
						angler.gameObject.SetActive(true);
					}

					if(jumping)//we were jumping and we landed on something
					{
						jumping=false;
						animator.SetBool("Jumping", false);
						jumpTarget.doTrajectory = false;
						rotateSpeed = 20.0f;
						movementTarget.enabled=false;
						movementTarget.transform.position= transform.position + (transform.forward*0.2f);
						lookAtPos = transform.position + (transform.forward*0.2f);
						angler.gameObject.SetActive(true);
					}
					envVars envVariables = floorPlane.transform.gameObject.GetComponent<envVars>();
					if(envVariables != null)
					{
						if(envVariables.isPlatform)//RUNGY change this to use env flags instead of string comparison
						{
							transform.parent=floorPlane.transform;
							if(movementTarget.transform.parent!=floorPlane.transform || lookAtTarget.transform.parent!=floorPlane.transform)
							{
								movementTarget.transform.parent=floorPlane.transform;
								lookAtTarget.transform.parent=floorPlane.transform;
								movementTarget.transform.position= transform.position + (transform.forward*0.2f);
								lookAtPos = transform.position + (transform.forward*0.2f);
							}
						}
						else
						{
							transform.parent=null;
							movementTarget.transform.parent=null;
							lookAtTarget.transform.parent=null;
						}
					}
				}

				//////////////////////////////
				
				//HORIZONTAL/LATERAL DETECTION
				
				//////////////////////////////
				
				//make a forward ray, that's about waist high
				ray.direction = transform.forward;
				//on meter up from our feet
				ray.origin = transform.position+transform.up;
				target = new RaycastHit();
				for(int i=0;i<currentZone.zones.Count;i++)
				{
					for(int f=0 ; f<currentZone.zones[i].floors.Count ; f++)
					{
						//If a floor is the closest...
						if (currentZone.zones[i].floors[f].Raycast (ray, out hit, 0.8f)) 												
						{
							animator.SetBool("Idling", true);
							movementTarget.enabled = false;

							if(jumping)
							{
								jumping=false;
								animator.SetBool("Jumping", false);
								jumpTarget.doTrajectory = false;
								rotateSpeed = 20.0f;
							}
						}
					}
				}

				if(transform.parent == floorPlane.transform)
				{
					lookAtPos = movementTarget.transform.position;
				}
				lookAtPos.y = transform.position.y;
				Quaternion tempRot = transform.rotation; 	//save current rotation
				transform.LookAt(lookAtPos);						
				Quaternion hitRot = transform.rotation;		// store the new rotation
				// now we slerp orientation
				transform.rotation = Quaternion.Slerp(tempRot, hitRot, Time.deltaTime * rotateSpeed);

				Vector3 gv = new Vector3(0.0f,fallspeed*Time.deltaTime,0.0f);
				transform.position -= gv;


				/////////////////////////////

				/// LEFT MOUSE CLICK

				/////////////////////////////
				if ( ! Input.GetKey(KeyCode.LeftAlt))//if we are not using the ALT key(camera control)...
				{
					// RUNGY THIRD PERSON INTERCEPT
					// floor goal	
					if(Input.GetMouseButton(0))//is the left mouse button being clicked?
					{
						//RUNGY, this is where you setup your close third person controller
						if(useThird)
						{

						}
						else
						{
							foundFloor = false;///declaring it here because we use it elsewhere as well
							destFloor = null;
							closest=210.0f; //same for this
							ray = Camera.main.ScreenPointToRay (Input.mousePosition);
							for(int i=0;i<currentZone.zones.Count;i++)
							{
								for(int f=0 ; f<currentZone.zones[i].floors.Count ; f++)															
								{
									//If a floor is the closest...
									if (currentZone.zones[i].floors[f].Raycast (ray, out hit, 500)) 												
									{
										float dist = Vector3.Distance(hit.point,ray.origin);
										if(!foundFloor)
										{
											target = hit;
											closest = dist;
											foundFloor = true;
											destFloor=currentZone.zones[i].floors[f];
										}
										//Rungy this is wrong, it could potentially pick an equals to over a less than value
										if(dist <= closest)
										{
											closest = dist;
											target = hit;
											destFloor=currentZone.zones[i].floors[f];
										}
									}
								}
							}
							if(destFloor!=null)
							{
								clickedFloor = destFloor;
								attackTarget.transform.position=target.point;
								//Rungy, this is where you need to use a cursor object with a character controller on it
								//placing the controller on the spot wherre you hit, getting the name of the object...doing another downward trce to it for the actual position
								
								movementTarget.transform.position = target.point;//mark it where it hit
								movementTarget.transform.up = target.normal;
								movementTarget.enabled=true;

								envVars envVariables = destFloor.transform.gameObject.GetComponent<envVars>();
								if(envVariables != null)
								{
									if(envVariables.isPlatform)//RUNGY change this to use env flags instead of string comparison
									{
										movementTarget.transform.parent=destFloor.transform;
									}
								}

								lookAtPos = target.point;
								lookAtPos.y = transform.position.y;
								wasAttacking = false;//we're moving now, not attacking
							}
						}
					}
				}
				Debug.DrawLine ((movementTarget.transform.position + transform.up*2), movementTarget.transform.position);//useful for visuals in editor
				lookAtTarget.transform.position = lookAtPos;

				lastDir = (transform.position - lastPosition).normalized;
				lastPosition = transform.position;

				if(Input.GetKey(KeyCode.UpArrow))
				{
					moveType = 0.0f;
					wasAttacking=false;
				}
				else if(Input.GetKey(KeyCode.DownArrow))
				{
					moveType = 2.0f;
					wasAttacking=false;
				}
				else if(Input.GetKey(KeyCode.LeftArrow))
				{
					moveType = 3.0f;
					wasAttacking=false;
				}
				else if(Input.GetKey(KeyCode.RightArrow))
				{
					moveType = 1.0f;
					wasAttacking=false;
				}
			}
			else//MOUNTED
			{
				animator.SetBool("Idling", true);
				//still need thesed variables even if we're on a mount
				lastDir = (transform.position - lastPosition).normalized;
				velocityPS = Vector3.Distance(transform.position,lastPosition)/Time.unscaledDeltaTime;
				lastPosition = transform.position;
			}
		}
	}




	void OnGUI()
	{
		GUI.Label (new Rect (10, 5,1000, 20), tempString);
	}

	void CheckForHit(EnemyController en, ASCollCheck ac)
	{
		//AngleRanged
		if(en.stats.health>0.0f)
		{
			float angle=ac.conicAngle/2;
			Vector3 tDelta = en.gameObject.transform.position - transform.position;
			float tAngle = Vector3.Angle(transform.forward,tDelta);
			if (tAngle< 0) tAngle*=-1;
			if (tAngle<angle)
			{
				if(Vector3.Distance(transform.position, en.gameObject.transform.position)<ac.range)
				{
					//we have a hit
					//AngleRanged

					//Damage
					//RUNGY, for chained hits you will need to change this to use a hit index
					float damage1 = ac.spawns[0].lowValue;
					float damage2 = ac.spawns[0].highValue;
					float damage =UnityEngine.Random.Range(damage1,damage2);
					en.stats.health-=damage;

					Transform tm = (Transform) Instantiate(hitReport , (en.gameObject.transform.position + new Vector3(0.0f,1.6f,0.0f)),Quaternion.identity);
					tm.gameObject.SetActive(true);
					Hit tmHit = tm.GetComponent<Hit>();
					tmHit.text = Mathf.RoundToInt(damage).ToString();
					Transform ph = (Transform) Instantiate(particleHit , (en.gameObject.transform.position + new Vector3(0.0f,1.5f,0.0f)),Quaternion.identity);
					ph.transform.LookAt(Camera.main.transform.position);
					ph.transform.position += (ph.transform.forward * 2.0f);
					ph.gameObject.SetActive(true);

					for(int spawnIndex=0;spawnIndex<ac.spawns.Length;spawnIndex++)
					{
						if(ac.spawns[spawnIndex].prefab!=null)
						{
							Transform tempSpawn = (Transform) Instantiate(ac.spawns[spawnIndex].prefab);
							Vector3 tempPos = tempSpawn.position;
							Quaternion tempRot = tempSpawn.rotation;
							if(ac.spawns[0].parent == ParentType.Self)
							{
								tempSpawn.parent=transform;
							}
							else if(ac.spawns[0].parent == ParentType.Target)
							{
								tempSpawn.parent=en.transform;
							}
							tempSpawn.localPosition = tempPos;
							tempSpawn.localRotation = tempRot;

							if(ac.spawns[0].parent == ParentType.None)
							{
								tempSpawn.parent=null;
								tempSpawn.position = transform.position;
							}
						}
					}
				}
			}
		}
		return;
	}



	void Jump()
	{
		float closest=210.0f; //same for this
		RaycastHit target = new RaycastHit();
		animator.SetBool("Idling", true);//stop running if we are
		jumping = true;
		ray = Camera.main.ScreenPointToRay (Input.mousePosition);
		if(useThird)
		{
			ray.origin = Camera.main.transform.position;
			ray.direction = (aimPoint.transform.position - game.mouseCam.transform.position).normalized;
		}
		for(int i=0;i<currentZone.zones.Count;i++)
		{
			for(int f=0 ; f<currentZone.zones[i].floors.Count ; f++)															
				
				//If a floor is the closest...
				if (currentZone.zones[i].floors[f].Raycast (ray, out hit, 500)) 												
			{
				float dist = Vector3.Distance(hit.point,ray.origin);
				if(!foundFloor)
				{
					closest = dist;
					foundFloor = true;
				}
				
				if(dist <= closest)
				{
					closest = dist;
					target = hit;
					destFloor=currentZone.zones[i].floors[f];
				}
			}
		}
		//RUNGY <<< here...you are unconditionally jumping if there are no hits....fix this
		transform.parent=null;
		jumpTarget.targetLocation = target.point;
		rotateSpeed = 5.0f;
		movementTarget.transform.position = target.point;
		movementTarget.transform.up = target.normal;
		movementTarget.enabled=true;
		envVars envVariables = destFloor.transform.gameObject.GetComponent<envVars>();
		if(envVariables != null)
		{
			if(envVariables.isPlatform)
			{
				transform.parent=destFloor.transform;
				if(movementTarget.transform.parent!=destFloor.transform || lookAtTarget.transform.parent!=destFloor.transform)
				{
					movementTarget.transform.parent=destFloor.transform;
					lookAtTarget.transform.parent=destFloor.transform;
				}
			}
			else
			{
				transform.parent=null;
				movementTarget.transform.parent=null;
				lookAtTarget.transform.parent=null;
			}
		}
		lookAtPos = target.point;
		animator.SetTrigger("Jump");
		animator.SetBool("Jumping", true);
		jumpTime = jumpTarget.InitLaunch()/90.0f;
		
		//ROUGHLY calculate offset in frames for height
		// we shorten if our target is higher
		if (target.point.y > transform.position.y) jumpTime *=.4f;
		// we lengthen if our target is lower
		if (target.point.y > transform.position.y) jumpTime *=1.9f;
		animator.SetFloat("JumpTime", jumpTime);
		angler.gameObject.SetActive(false);
	}
}
