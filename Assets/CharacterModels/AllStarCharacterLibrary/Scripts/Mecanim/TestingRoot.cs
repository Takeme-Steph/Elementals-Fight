using UnityEngine;
using System.Collections;

public class TestingRoot : StateMachineBehaviour 
{
	ASCLBasicController abc;
	
	// OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
	override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{
		abc = animator.gameObject.GetComponent<ASCLBasicController>();
	}



	// OnStateExit is called before OnStateExit is called on any state inside this state machine
	//override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
	//
	//}

	// OnStateMove is called before OnStateMove is called on any state inside this state machine
	override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{
		if (abc != null) 
		{
			Ray ray = new Ray ();
			ray.direction = animator.transform.up * -1;//make a downward ray, negative "up" is "down"
			ray.origin = animator.transform.position + animator.transform.up;//one meter up from our feet
			RaycastHit hit = new RaycastHit ();
			if (abc.floorPlane.Raycast (ray, out hit, 1.2f))
			{
				animator.transform.position = hit.point + animator.deltaPosition;
			}
		}
	}

	// OnStateIK is called before OnStateIK is called on any state inside this state machine
	override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{

	}

	// OnStateMachineEnter is called when entering a statemachine via its Entry Node
	//override public void OnStateMachineEnter(Animator animator, int stateMachinePathHash){
	//
	//}

	// OnStateMachineExit is called when exiting a statemachine via its Exit Node
	//override public void OnStateMachineExit(Animator animator, int stateMachinePathHash) {
	//
	//}
}
