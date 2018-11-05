using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleScript : MonoBehaviour {

    public bool shortlived = false;
    private CFDObjectScript cfd; //reference to main CFD script
    private float starttime;     //time of creation - for self-destruction purposes
    private Material m;          //reference to the material 

    void Start()
    // Initialise references and set start time
    {
        cfd = statics.getcfd();
        starttime = Time.time;
    }
    private bool dead = false;
    private float DeadTime = 0f;
    private void KillMe()
    //Stop emmission on object, keeping count in main CFD script correct
    //kill it 4 seconds later - let particles expire first
    {
        cfd.ParticleCount--;
        dead = true;                                        //mark as dead
        DeadTime = Time.time;
        ParticleSystem p = this.GetComponent<ParticleSystem>();
        ParticleSystem.EmissionModule e = p.emission;
        e.enabled = false;                                 //stop emitting particles
    }

    void Update ()
    // Per frame iteration
    {
        if (dead)
        {
            //kill it after 4 seconds
            if (shortlived)
            {
                if (Time.time > DeadTime + 1f) Destroy(gameObject);
            }
            else
            {
                if (Time.time > DeadTime + 4f) Destroy(gameObject);
            }
            return;
        }

        bool ok;
    
        Vector3 v = cfd.GetInterpolatedVector(transform.localPosition, out ok); //find velocity vector from main array
        if (!ok)
            KillMe(); //out of bounds - so destroy instantly
        else
            transform.localPosition += v * Time.deltaTime;   //apply velocity vector

        if (shortlived)
        {
            if (Time.time - starttime > cfd.ShortLivedParticleLifespan)
                KillMe(); //been alive too long, end it all now
        }
        else
        {
            if (Time.time - starttime > cfd.ParticleLifespan)
            KillMe(); //been alive too long, end it all now
        }
        
    }
}
