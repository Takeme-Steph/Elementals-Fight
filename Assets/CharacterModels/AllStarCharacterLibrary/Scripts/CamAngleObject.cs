using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class CamAngleObject : ScriptableObject
{
	public CamAngle camAngle;
	public CamAngleObject(CamAngle camAngle)
	{
		this.camAngle = camAngle;
	}
}
