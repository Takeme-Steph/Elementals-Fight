using UnityEngine;
using System.Collections;

public class Billboard2 : MonoBehaviour 
{
	Transform 			camera;
	public bool		flipFacing;

	// Use this for initialization
	void Start () 
	{
		camera = Camera.main.transform;
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		transform.rotation = camera.rotation;
	}
}
