using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TouchControlScript : MonoBehaviour
//Input handler - also handles positioning and scaling via controllers
{

    private CFDObjectScript cfd; //reference 
    void Start ()
    //Initialise
    {
        cfd = statics.getcfd();     //standard 'bodge' to pick up a reference to main CFD script
	}

    public GameObject LeftHandAnchor;   //reference to position of left hand
    public GameObject SelectPoint;      //reference to position of selection tip

    //various bits of internal state data
    private Vector3 dragstart;
    private float scale = 1.0f;         //scale of the viewer - not modifiable, but readable from outside via a Get

    private Vector3 reposv = Vector3.zero;
    private Vector3 actualv;
    private Vector3 targetv;
    private Vector3 startv;
    private float startscale;
    private float targetscale;
    private float starttime;
    private float finishtime;
    private bool animatingpos = false;
    private bool donerescale = false;
    private int stepscale = 0; //int scale system, 0 to 2

    private Vector3 lastpositionstepscale0 = new Vector3(0f, 0f, -100f);
    private Vector3 lastpositionstepscale1 = new Vector3(0f, 0f, -100f);


    // Update is called once per frame
    void Update()
    {
        if (!donerescale)
        {
            ReScale(1); donerescale = true; //initialise scale on first iteration
        }

        if (animatingpos) //animation system for smooth scale changes and movements
        {
            actualv = Vector3.LerpUnclamped(startv, targetv, (Time.time - starttime) / (finishtime - starttime));
            scale = Mathf.Lerp(startscale, targetscale, (Time.time - starttime) / (finishtime - starttime));
            if (Time.time >= finishtime) { animatingpos = false;}
        }

       transform.localScale = new Vector3(scale, scale, scale);  //set scale
       transform.position = actualv + reposv;   //set position (from a base position plus accumulated movement)

        //handle position/scale controls
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            cfd.MakeParticle(false);  //When right hand index trigger is clicked - make a particle
        }


        if (OVRInput.Get(OVRInput.Button.SecondaryHandTrigger))
        {
            cfd.MakeParticle(true);  //Keep making short-lived 'flame' particles while right hand inner trigger is down
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger))
        {
            dragstart = LeftHandAnchor.transform.localPosition;
        }
        
        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger))
        {
            if (!(OVRInput.Get(OVRInput.Button.Four) || OVRInput.GetDown(OVRInput.Button.Three)))
            {
                RePosition(LeftHandAnchor.transform.localPosition - dragstart);
            }
            dragstart = LeftHandAnchor.transform.localPosition;
        }

        if (OVRInput.GetDown(OVRInput.Button.Three))
        {
            ReScale(-1);
        }
        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            ReScale(+1);
        }
    }


    public void RePosition(Vector3 repos)
    //accumulate changes in position of left hand with drag button pressed
    {
        reposv -= 50f * repos * Mathf.Sqrt(scale);
        return;
    }

    public float GetScale()
    {
        return scale;
    }

    void ReScale(int intscale)
    //rescale the viewer on request - not well documented I know! Stolen from some old code of mine :-)
    {
        int oldstepscale = stepscale;
        stepscale += intscale;

        if (stepscale > 2) stepscale = 2; //max zoom in
        if (stepscale < 0) stepscale = 0; //max zoom out

        if (stepscale == 2) targetscale = 1f;
        if (stepscale == 1) targetscale = 64f;
        if (stepscale == 0) targetscale = 256f;

        startv = transform.position;

        startscale = transform.localScale.x;
        starttime = Time.time;
        finishtime = Time.time + .5f;
        animatingpos = true;

        if (stepscale == 2)
        {
            if (oldstepscale == 1)
            {
                lastpositionstepscale1 = actualv + reposv;
                targetv = SelectPoint.transform.position + new Vector3(0f, 0f, -5f);
            }
        }
        if (stepscale == 1)
        {
            if (oldstepscale == 0)
            {
                lastpositionstepscale0 = actualv + reposv;
                targetv = SelectPoint.transform.position + new Vector3(0f, 0f, -25f);
            }
            else
            {
                targetv = lastpositionstepscale1;
            }
        }
        if (stepscale == 0)
        {
            if (oldstepscale == 1) targetv = lastpositionstepscale0;
            else
                targetv = new Vector3(0f,0f,-100f);
        }
        reposv = Vector3.zero;

    }

}
