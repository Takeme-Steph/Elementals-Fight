using UnityEngine;
using System.Collections;

public class CamScript : MonoBehaviour 
{
	public string filename = "";
	// Use this for initialization
	void Start () 
	{
	
	}
	
	// Update is called once per frame
	void Update () 
	{
	
	}
	
	void OnPostRender()
	{
		if (filename !="")
		{
			Texture2D tex = new Texture2D(2048, 2048);
			tex.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0);
			tex.Apply();
			System.IO.File.WriteAllBytes( filename , tex.EncodeToPNG());	
			filename = "";
		}
	}
	
}
