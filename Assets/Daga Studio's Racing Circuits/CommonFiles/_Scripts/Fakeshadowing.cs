using UnityEngine;
using System.Collections;

public class Fakeshadowing : MonoBehaviour {

public Light lightObj;
 
void  OnTriggerEnter (){
    lightObj.enabled = false;
}
 
void  OnTriggerExit (){
    lightObj.enabled = true;
}

}









