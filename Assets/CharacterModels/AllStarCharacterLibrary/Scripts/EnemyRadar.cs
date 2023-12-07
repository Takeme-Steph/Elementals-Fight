using UnityEngine;
using System.Collections;

public class EnemyRadar : MonoBehaviour 
{
	public Transform 	target;
	public GameObject 	arrow;
	private float		alpha = 1.0f;
	public float		minDistance= 10.0f;
	private Color		color;
	private Color		colorDot;
	public string		property;
	public Stats		stats;

	// Use this for initialization
	void Start () 
	{
		stats = target.parent.gameObject.GetComponentInParent<Stats> ();

		if(arrow.GetComponent<Renderer>().material.HasProperty(property))
		{
			color = arrow.GetComponent<Renderer>().material.GetColor(property);
			colorDot = target.gameObject.GetComponent<Renderer>().material.GetColor(property);
		}
		else
		{
			property = "";
		}
	}
	
	// Update is called once per frame
	void Update () 
	{
		if (target) 
		{
			//lookat
			transform.rotation = Quaternion.FromToRotation(-Vector3.forward, (new Vector3(target.position.x, transform.position.y, target.position.z) - transform.position).normalized);
			
			float distance = (target.position - transform.position).magnitude;
			
			if(distance < minDistance)
			{
				alpha = Mathf.Lerp(alpha, 0.0f, Time.deltaTime * 20.0f);
			}
			else
			{
				alpha = Mathf.Lerp(alpha, 1.0f, Time.deltaTime * 20.0f);
			}
			
			if(!string.IsNullOrEmpty(property))
			{
				color.a = Mathf.Clamp(alpha, 0.0f, 1.0f);
				colorDot.a = Mathf.Clamp(1.0f-alpha, 0.0f, 1.0f);
				
				arrow.GetComponent<Renderer>().material.SetColor(property, color);
				target.gameObject.GetComponent<Renderer>().material.SetColor(property, colorDot);

			}
			if (stats.health <= 0.0f) 
			{
				Destroy(target.gameObject);
				Destroy(gameObject);
			}
			
		}
	}
}
