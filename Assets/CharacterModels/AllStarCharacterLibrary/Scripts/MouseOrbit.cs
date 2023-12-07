using UnityEngine;
using System.Collections;

public class MouseOrbit : MonoBehaviour 
{
	public Transform 		target;
	public float 			distance = 15f;
	public bool				useCamAngle = false;
	public Game				game;
	public bool				mounted;
	float 					xSpeed = 4.0f;
	float 					ySpeed = 1.0f;
	public float 			x = 15.0f;
	public float 			y = 5.0f;
	public CamAngleObject	camObject;
	public CamAngle			camAngle;
//CURRENT VERSION
	public void Start () 
	{
		game = FindObjectOfType(typeof(Game)) as Game;
		if(camObject!=null)
		{
			if(useCamAngle)
			{
				camAngle = camObject.camAngle;
				distance = camAngle.distance;
				Vector3 tempVec = game.player.position + (game.player.forward * -camAngle.xzDistance);
				tempVec.y = camAngle.positionHeight + game.abc.transform.position.y;
				transform.position = tempVec;

                //RUNGY, this is wrong...you should set the target pan instead
				//transform.forward = ((tempVec + transform.up * camAngle.cameraTargetHeight)-transform.position).normalized;
				if(!camAngle.usePlayerForward)
				{
					transform.rotation = camAngle.rotation;
				}
			}
		}

	    Vector3 angles = transform.eulerAngles;
    	x = angles.y;
    	y = angles.x;
		transform.rotation = Quaternion.Euler( y, x, 0.0f);
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		if(!game.abc.mounted && !game.abc.useThird)
		{
			distance += Input.GetAxis("Mouse ScrollWheel") * 5 ;
			
			if ( Input.GetKey(KeyCode.LeftAlt))
			{
				if (Input.GetMouseButton(1))
				{
					distance += Input.GetAxis("Mouse Y") * 0.5f;
				}
				
				if (Input.GetMouseButton(0))
				{
					x += Input.GetAxis("Mouse X") * xSpeed*3;
					y -= Input.GetAxis("Mouse Y") * ySpeed*8;
					y = ClampAngle(y);
					x = ClampAngle(x);
					transform.rotation = Quaternion.Euler( y, x, 0.0f);
				}
				

				if (Input.GetMouseButton(2))
				{
					float x2 = Input.GetAxis("Mouse X");
					float y2 = Input.GetAxis("Mouse Y");
					target.transform.position += transform.right * (-x2*0.2f);
					target.transform.position += transform.up * (-y2 *0.2f);
				}

			}

			if(camObject!=null)
			{
				if(useCamAngle)
				{
					if(camAngle.usePlayerForward==true)
					{
						/*
						Vector3 tempVec = game.player.position;
						tempVec += game.player.forward * -(camAngle.xzDistance);
						tempVec.y = camAngle.positionHeight + game.abc.transform.position.y;
						transform.position = tempVec;
						transform.forward = ((game.player.transform.position + transform.up * camAngle.cameraTargetHeight)-transform.position).normalized;
						transform.position += camAngle.localOffset.x*transform.right + camAngle.localOffset.y*transform.up + camAngle.localOffset.z*transform.forward;
						*/
					}
				}
			}
			transform.position = target.transform.position - (transform.forward * distance);
		}
	}
	
	float ClampAngle (float angle) 
	{
		if (angle < -360)
			angle += 360;
		if (angle > 360)
			angle -= 360;
		return angle;
	}
	
void OnGUI()
	{

		string tempString = "leftALT+LMB to orbit,   leftALT+RMB to zoom,   leftALT+MMB to pan";
		GUI.Label (new Rect (10, 25,1000, 20), tempString);
	}
}
