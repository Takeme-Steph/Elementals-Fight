using UnityEngine;
using System.Collections;

public class CamZone : MonoBehaviour 
{
	public CamAngleObject	camAngleObject;
	public CamAngle			camAngle;

	// Use this for initialization
	void Start () 
	{
		camAngle = camAngleObject.camAngle;
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
	
	}
}
