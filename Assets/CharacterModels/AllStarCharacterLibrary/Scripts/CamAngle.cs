using UnityEngine;
using System.Collections;

[System.Serializable]
public class CamAngle
{
	//lock to the players forward direction?
	public bool			usePlayerForward=false;

	//if TRUE, the cameras position & focus are dragged, like being dragged by a rope, instead of hard linked
	public bool			dragCamera = false;

	//false for top down or a downward view, true for things like flying, swimming, or near 1st person views
	public bool			useVerticalPlanes=false;

	//might want our camera to be higher or more to the right, anyplace that is directly centered on the player
	public Vector3		localOffset;
	public Vector3		aimOffset;
	//for player.transform.forward usage(3rd person locked to player forward direction)
	public float 		fov;
	public float		xzDistance;			//-XZDistance*player.transform.forward
	public float		positionHeight;
	public float		cameraTargetHeight;

	//for top down or scroller usage
	public Quaternion	rotation;
	public float		distance;
}
