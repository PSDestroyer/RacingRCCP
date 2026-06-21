using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DriverControllerScript : MonoBehaviour {

	public Animator animator;


	void Start()

	{
		if (animator == null)
			animator = GetComponent<Animator>();
	}

	void Update()
	{
		animator.SetFloat("Steer",(Input.GetAxis("Horizontal")+1f)/2f);
	}

}
