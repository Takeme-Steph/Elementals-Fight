using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

public class AbilityObject : ScriptableObject
{
	public ASAbility ability;
	public AbilityObject(ASAbility ability)
	{
		this.ability = ability;
	}
}
