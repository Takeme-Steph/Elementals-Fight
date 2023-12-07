using UnityEngine;
using System.Collections;

public class RadarDot : MonoBehaviour 
{
	public Transform	obj;
	public Stats		stats;

	void Start()
	{
		stats = GetComponentInParent<Stats> ();
	}

	void LateUpdate () 
	{
		Vector3 objPos = transform.position;
		objPos.y = -163.0f;
		transform.position = objPos;
	}
}
