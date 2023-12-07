/*
* Original Author: Chris Campbell - www.iforce2d.net
* Copyright (c) 2006-2011 Erin Catto http://www.box2d.org
* 
* Unity adaptation by Ranjeet Singhal, Sexysidekicks.com
* persuent to the following disclaimer
* 
* This software is provided 'as-is', without any express or implied
* warranty.  In no event will the authors be held liable for any damages
* arising from the use of this software.
* Permission is granted to anyone to use this software for any purpose,
* including commercial applications, and to alter it and redistribute it
* freely, subject to the following restrictions:
* 1. The origin of this software must not be misrepresented; you must not
* claim that you wrote the original software. If you use this software
* in a product, an acknowledgment in the product documentation would be
* appreciated but is not required.
* 2. Altered source versions must be plainly marked as such, and must not be
* misrepresented as being the original software.
* 3. This notice may not be removed or altered from any source distribution.
*/

using UnityEngine;
using System.Collections;
#pragma warning disable

public class TrajectoryTarget : MonoBehaviour 
{
	public Vector3 	targetLocation;
	public bool 	doTrajectory;
	public float 	maxHeight = 5;
	public float	maxLength = 10;
	public float	dropSpeed = 1.0f;

	Vector3 startingPosition;
	Vector3 normalDelta;
	Vector3 gravity;
	Vector3 startingVelocity;
	float ourTimer = 0.0f;

	RaycastHit hit;
	Ray ray;
	float dist = 10.0f;


	// Use this for initialization
	public float InitLaunch () 
	{
		gravity = new Vector3(0.0f, (-1.0f *dropSpeed) , 0.0f);
		ourTimer = 0.0f;
		startingPosition = transform.position;

		normalDelta = Vector3.Normalize(targetLocation - startingPosition);
		float closest = Mathf.Abs(Mathf.Min( Vector3.Distance(targetLocation,startingPosition),maxLength));
		targetLocation = closest*normalDelta;


		float lStep = 1/maxLength;
		float targetHeight = (lStep*closest)* maxHeight;
		float offset = Mathf.Min(targetHeight , normalDelta.y*maxHeight);
		targetHeight += offset * 0.5f;
		targetHeight = Mathf.Max(.2f,targetHeight);
		closest += offset * 0.5f;


		float verticalVelocity = calculateVerticalVelocityForHeight(targetHeight);

		startingVelocity= new Vector3(0.0f,verticalVelocity,0.0f);//only interested in vertical here

		float timestepsToTop = getTimestepsToTop( startingVelocity );

		float horizontalVelocity = closest / timestepsToTop * 60.0f;//horizontal velocity per second, timesteps are measured by 60th's of a second

		Vector3 horizontalDelta = (normalDelta * horizontalVelocity)/2.0f;
		horizontalDelta *= 1.0f+ (Vector3.Dot(normalDelta,(transform.up*-1))*0.8f);

		
		startingVelocity = new Vector3(horizontalDelta.x, verticalVelocity, horizontalDelta.z);
		doTrajectory = true;
		return(timestepsToTop);
	}
	
	// Update is called once per frame
	void Update () 
	{
		if (doTrajectory)
		{
			ourTimer += Time.unscaledDeltaTime;
			float frames = ourTimer/ 0.0026f;//.0026 is the time slice per second for a frame of animation made @30fps...this could be a problem for people who author content at different speeds
			transform.position = getTrajectoryPoint(frames);
		}
	}
	
    Vector3 getTrajectoryPoint(float n)//frames
    {
        float t = 1 / 60.0f;
        Vector3 stepVelocity = t * startingVelocity; // m/s
        Vector3 stepGravity = t * t * gravity; // m/s/s
        return startingPosition + n * stepVelocity + 0.5f * (n*n+n) * stepGravity;
    }

    //find out how many timesteps it will take for projectile to reach maximum height
    float getTimestepsToTop( Vector3 startingVelocity )
    {
        float t = 1 / 60.0f;
        Vector3 stepVelocity = t * startingVelocity; // m/s
        Vector3 stepGravity = t * t * gravity; // m/s/s
        float n = -stepVelocity.y / stepGravity.y - 1;
        return n;
    }

    //find out the maximum height for this parabola
    float getMaxHeight( Vector3 startingPosition, Vector3 startingVelocity )
    {
        if ( startingVelocity.y < 0 )
            return startingPosition.y;
        float t = 1 / 60.0f;
        Vector3 stepVelocity = t * startingVelocity; // m/s
        Vector3 stepGravity = t * t * gravity; // m/s/s
        float n = -stepVelocity.y / stepGravity.y - 1;		
        return startingPosition.y + n * stepVelocity.y + 0.5f * (n*n+n) * stepGravity.y;
    }

    //find the initial velocity necessary to reach a specified maximum height
    float calculateVerticalVelocityForHeight( float desiredHeight )
    {
        if ( desiredHeight <= 0 )
        return 0;
        float t = 1 / 60.0f;
        Vector3 stepGravity = t * t * gravity; // m/s/s

        //quadratic equation setup
        float a = 0.5f / stepGravity.y;
        float b = 0.5f;
        float c = desiredHeight;
		float v = ( -b - Mathf.Sqrt( b*b - 4*a*c ) ) / (2*a);
		if ( v < 0 )
		v = ( -b + Mathf.Sqrt( b*b - 4*a*c ) ) / (2*a);
		
		return v * 60.0f;
    }
}