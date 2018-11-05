using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml;

public class CFDObjectScript : MonoBehaviour
//Main class with single instance, to be attached to gameobject at root. Handles CFD simulation/visulisation
{

    //Public data modifiable in Unity Editor
    public int XDim = 100, YDim = 100, ZDim = 100, TimeSteps = 10; //dimensions of data from CFD
    public GameObject SelectionPoint;   //the object used to select / release particles
    public GameObject PrefabArrow;      //prefab for the velocity arrows
    public GameObject PrefabParticle;   //prefab for the particles
    public GameObject PrefabParticleShortLived; //shortlived version for continuous flow indication
    public GameObject EdgePrefab;
    public GameObject CornerCubePrefab;
    public TouchControlScript tcs;

    public int ParticleCount = 0;       //not an input, but useful to view in editor as an output

    //Settings for simulation
    public float ArrowSizeScaleFactor = .05f;        //Base size for velocity arrows
    public float ParticleVelocityScaleFactor = 20f;  //Base speed for particles
    public float TipSizeScaleFactor = 1f;

    public float TimeFactor = 0.1f;         //TimeSteps per second
    public float ParticleLifespan = 10f;    //in seconds
    public float ShortLivedParticleLifespan = 1f; //particles for continuous emission
    public bool trilinear = true;           //Use trilinear interpolation for particle velocity? If false, the velocity array is sampled not interpolated from 
    public bool ShowArrows = true;
    public int VectorIndex = 0;             //the vector parameter to display as arrows. By default 0 as we only have one set
    public int ScalarIndex = 0;             //the scalar parameter to display as pointer size (eventually sound as well?)

    //internal data and objects
    private Vector3[,,,,] VectorArray;
    private float[,,,,] ScalarArray;             //arrays of data from CFD. 5 dimensions! Indices are x,y,z,time,param
    private List<float[,,,]> ScalarArrayList = new List<float[,,,]>();
    private List<float> VectorScaleFactors = new List<float>(); //These hold the maximum magnitude of vectors for each parameter - used to scale the visualisation. 
    private List<float> ScalarScaleFactors = new List<float>(); //Ditto for scalars

    private List<string> VectorNames = new List<string>();
    private List<string> ScalarNames = new List<string>();

    private GameObject[,,] arrows;                  //array holding instantiated velocity arrows
    private System.Random rnd = new System.Random(); //random number generator

    private float CurrentTime = 0f;         //Time in this iteration
    private float StartTime = 0f;           //Time of simulation start
    private int LastIntTime, NextIntTime;   //Floor and Ceiling timeslice indices
    private float tf, omtf;                 //Fraction of time between timeslices, and one minus that (om)



    void Start()
    // Initialise
    {
        System.Diagnostics.Stopwatch t = new System.Diagnostics.Stopwatch();
        t.Start();
        print("Starting setup...");

        //Set up parameter lists
        VectorNames.Clear();
        VectorNames.Add("Velocity");
        VectorScaleFactors.Add(0f);

        ScalarNames.Clear();
        ScalarNames.Add("Pressure");
        ScalarScaleFactors.Add(0f);

        //Set up main arrays
        SetUpData(33, 33, 33, 2);

        //Read two sample files, at timeslice positions 0 and 1
        ReadXML("d:\\CFD\\Poiseuille\\VAR_poiseuille_0_0.vtr",0);
        ReadXML("d:\\CFD\\Poiseuille\\VAR_poiseuille_0_1.vtr", 1);

        //Set up arrow grid and edge markers
        MakeArrows();
        MakeEdgeMarkers();

        //Do an initial orientate/size of arrows - this happens each frame anyway
        OrientateArrows();

        //report time taken for setup
        t.Stop();
        print("Setup complete, elapsed time " + t.ElapsedMilliseconds + "ms");
    }

    private void SetUpData(int x, int y, int z, int t)
    {
        XDim = x;
        YDim = y;
        ZDim = z;
        TimeSteps = t;
        VectorArray = new Vector3[x, y, z, t, VectorNames.Count];
        ScalarArray = new float[x, y, z, t, ScalarNames.Count];

    }

    public string NormalizeWhiteSpaceForLoop(string input)
    //replaces repeat spaces with a single space - stolen from https://stackoverflow.com/questions/6442421/c-sharp-fastest-way-to-remove-extra-white-spaces/37592018
    {
        int len = input.Length,
            index = 0,
            i = 0;
        var src = input.ToCharArray();
        bool skip = false;
        char ch;
        for (; i < len; i++)
        {
            ch = src[i];
            switch (ch)
            {
                case '\u0020':
                case '\u00A0':
                case '\u1680':
                case '\u2000':
                case '\u2001':
                case '\u2002':
                case '\u2003':
                case '\u2004':
                case '\u2005':
                case '\u2006':
                case '\u2007':
                case '\u2008':
                case '\u2009':
                case '\u200A':
                case '\u202F':
                case '\u205F':
                case '\u3000':
                case '\u2028':
                case '\u2029':
                case '\u0009':
                case '\u000A':
                case '\u000B':
                case '\u000C':
                case '\u000D':
                case '\u0085':
                    if (skip) continue;
                    src[index++] = ch;
                    skip = true;
                    continue;
                default:
                    skip = false;
                    src[index++] = ch;
                    continue;
            }
        }

        return new string(src, 0, index);
    }


    private void ReadXML(string fname, int timeslice)
    //Read an xml file into arrays for the given timeslice
    //This is NOT a general XML reader - it ignores everything except dataarray elements
    {
        bool indata = false;    //flags up a relevant value block
        bool vector = false;    //flags up vector as opposed to scalar data
        int pindex=-1;          //index of vector or scalar in my arrays
        
        XmlTextReader reader = new XmlTextReader(fname);
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element: // The node is an element.
                    switch (reader.Name.ToUpper())
                    {
                        case "DATAARRAY":
                            string paramname = reader.GetAttribute("Name");
                            string numcomp = reader.GetAttribute("NumberOfComponents"); //if this is 3, it's a vector

                            if (string.IsNullOrEmpty(numcomp)) vector = false;
                            else
                            {
                                if (numcomp == "3")
                                    vector = true;
                                else
                                {
                                    Debug.LogError("Error in NumberOfComponents - should be absent, or '3'");
                                }
                                
                            }
                            if (vector) //find index from my list of parameter names
                            {
                                pindex = VectorNames.IndexOf(paramname);
                                if (pindex == -1)
                                {
                                    Debug.LogWarning("Unrecognized parameter name " + paramname + " - ignoring");
                                }
                                else indata = true; //next value block is to be read
                            }
                            else
                            {
                                //read as scalar array                      
                                pindex = ScalarNames.IndexOf(paramname); //find index from my list of parameter names
                                if (pindex == -1)
                                {
                                    Debug.LogWarning("Unrecognized parameter name " + paramname + " - ignoring");
                                }
                                else indata = true;     //next value block is to be read
                            }
                            break;
                    }
                    break;
                case XmlNodeType.Text:
                    if (indata)
                    {
                        if (vector)
                        {
                            //read vector data in
                            print("Reading Vector param " + VectorNames[pindex]);
                            System.Diagnostics.Stopwatch t = new System.Diagnostics.Stopwatch();
                            t.Start();

                            string s = reader.Value;   //get text as one big string
                            s = s.Replace('\n', ' ');   // replace newlines with spaces
                            s = NormalizeWhiteSpaceForLoop(s);  //remove repeat spaces
                            string[] v = s.Split(' '); //v array now holds all the individual strings - but first and last are empty on test file for some reason
                            int start;
                            if (v[0].Length == 0) start = 1; else start = 0; //skip first empty if there is one

                            float maxmagnitude = VectorScaleFactors[pindex]; //get largest magnitude so far. Work with copy of this in the loop for speed
                                                                             //can't just read this from a file - has to be maximum over all time slices!
                            for (int i = start; i < (XDim*YDim*ZDim)*3; i += 3) //last one never gets read. i=start avoids initial blank 
                            {
                                int ii = i / 3;   //vector index - reading three floats per vector
                                Vector3 vec = new Vector3(float.Parse(v[i]), float.Parse(v[i + 1]), float.Parse(v[i + 2]));
                                float m = vec.magnitude;
                                if (m > maxmagnitude) maxmagnitude = m;
                                int x = ii % XDim;          //x,y,z indices - ordering is a guess!
                                int y = (ii / XDim) % YDim;
                                int z = (ii / (XDim * YDim));
                                VectorArray[x, y, z, timeslice, pindex] = vec;
                            }
                            VectorScaleFactors[pindex] = maxmagnitude; //put back possibly increased maximum magnitude
                            indata = false;
                            t.Stop();
                           
                            print("Finished vector read, took " + t.ElapsedMilliseconds+ "ms");
                        }
                        else
                        {
                            print("Reading Scalar param " + ScalarNames[pindex]);
                            System.Diagnostics.Stopwatch t = new System.Diagnostics.Stopwatch();
                            t.Start();
                            string s = reader.Value;   //get text as one big string
                            s = s.Replace('\n', ' ');   // replace newlines with spaces
                            s = NormalizeWhiteSpaceForLoop(s);  //remove repeat spaces
                            string[] v = s.Split(' '); //v array now holds all the individual strings - but first and last are empty on test file for some reason
                            int start;
                            if (v[0].Length == 0) start = 1; else start = 0; //skip first empty if there is one
                            float maxmagnitude = ScalarScaleFactors[pindex];
                            for (int i = start; i < (XDim * YDim * ZDim); i++)
                            {
                                float value = float.Parse(v[i]);
                                if (value > maxmagnitude) maxmagnitude = value;
                                int x = i % XDim;
                                int y = (i / XDim) % YDim;
                                int z = (i / (XDim * YDim));
                                ScalarArray[x, y, z, timeslice, pindex] = value;
                            }
                            ScalarScaleFactors[pindex] = maxmagnitude; //put back possibly increased maximum magnitude
                            indata = false;
                            t.Stop();

                            print("Finished scalar read, took " + t.ElapsedMilliseconds + "ms");
                        }
                    }
                    
                    break;
            }
        }
    }

    private void MakeArrows()
    //Create velocity arrow markers. At present hardwired in as a 10x10x10 grid
    {
        arrows = new GameObject[10, 10, 10];
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                for (int z = 0; z < 10; z++)
                {
                    arrows[x, y, z] = Instantiate(PrefabArrow, transform, false);                      //make arrow, with this object as parent
                    arrows[x, y, z].transform.localPosition = new Vector3(((float)(x*XDim))/10f+((float)XDim)/20f,
                                                                          ((float)(y * YDim)) / 10f + ((float)YDim) / 20f, 
                                                                          ((float)(z * ZDim)) / 10f + ((float)ZDim) / 20f);  //move to grid position
                }
    }

    private void MakeEdgeMarkers()
    //make the frame around the structured grid
    {
        //edges
        GameObject g = Instantiate(EdgePrefab,transform);
        g.transform.localPosition = new Vector3(0f, 0f, ZDim/2f);
        g.transform.localScale = new Vector3(1f, ZDim/2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localPosition = new Vector3(0f, YDim, ZDim/2f);
        g.transform.localScale = new Vector3(1f, ZDim/2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localPosition = new Vector3(XDim, 0f, ZDim / 2f);
        g.transform.localScale = new Vector3(1f, ZDim/2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localPosition = new Vector3(XDim,YDim, ZDim / 2f);
        g.transform.localScale = new Vector3(1f, ZDim/2f, 1f);

        g = Instantiate(EdgePrefab, transform);
        g.transform.localPosition = new Vector3(XDim/2f, 0f,0f);
        g.transform.localEulerAngles = new Vector3(90f, 0f, 90f);
        g.transform.localScale = new Vector3(1f, YDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = new Vector3(90f, 0f, 90f);
        g.transform.localPosition = new Vector3(XDim / 2f, YDim, 0f);
        g.transform.localScale = new Vector3(1f, YDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = new Vector3(90f, 0f, 90f);
        g.transform.localPosition = new Vector3(XDim / 2f, 0f, ZDim);
        g.transform.localScale = new Vector3(1f, YDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = new Vector3(90f, 0f, 90f);
        g.transform.localPosition = new Vector3(XDim / 2f, YDim, ZDim);
        g.transform.localScale = new Vector3(1f, YDim / 2f, 1f);


        g = Instantiate(EdgePrefab, transform);
        g.transform.localPosition = new Vector3(0f, YDim/2f, 0f);
        g.transform.localEulerAngles = Vector3.zero;
        g.transform.localScale = new Vector3(1f, ZDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = Vector3.zero;
        g.transform.localPosition = new Vector3(XDim , YDim / 2f, 0f);
        g.transform.localScale = new Vector3(1f, ZDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = Vector3.zero;
        g.transform.localPosition = new Vector3(0f, YDim / 2f, ZDim);
        g.transform.localScale = new Vector3(1f, ZDim / 2f, 1f);
        g = Instantiate(EdgePrefab, transform);
        g.transform.localEulerAngles = Vector3.zero;
        g.transform.localPosition = new Vector3(XDim, YDim / 2f, ZDim);
        g.transform.localScale = new Vector3(1f, ZDim / 2f, 1f);

        //corners
        g = Instantiate(CornerCubePrefab,transform); g.transform.localPosition=new Vector3(0f,0f,0f);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(XDim, 0f, 0f);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(0f, YDim, 0f);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(XDim, YDim, 0f);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(0f, 0f, ZDim);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(XDim, 0f, ZDim);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(0f, YDim, ZDim);
        g = Instantiate(CornerCubePrefab, transform); g.transform.localPosition = new Vector3(XDim, YDim, ZDim);

    }

    private void OrientateArrows()
    //get arrow rotation and size from array at current time point and apply it to arrow objects
    {
        float xsf, ysf, zsf;
        xsf = ((float)XDim) / 10f;
        ysf = ((float)XDim) / 10f;
        zsf = ((float)XDim) / 10f;

        float scalefactor = ArrowSizeScaleFactor / VectorScaleFactors[VectorIndex];  //rescale factor for visualisation

        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                for (int z = 0; z < 10; z++)
                {
                    int xi = (int)(((float)x) * xsf + xsf / 2f);
                    int yi = (int)(((float)y) * ysf + ysf / 2f);
                    int zi = (int)(((float)z) * zsf + zsf / 2f);

                    Vector3 v = VectorArray[xi,yi,zi,LastIntTime, VectorIndex] * omtf 
                        + VectorArray[xi,yi,zi, NextIntTime, VectorIndex] * tf;       //find sample at correct timepoint
                    float scale = v.magnitude * scalefactor;                                                                            //scale is proportional to magnitude of vector 
                    if (scale > .001f) arrows[x, y, z].transform.rotation = Quaternion.LookRotation(v, Vector3.up);                     //rotate in direction of vector (avoiding error for zero vectors)
                    arrows[x, y, z].transform.localScale = new Vector3(scale, scale, scale);                                            //scale accordingly
                    if (ShowArrows)
                    {
                        if (!(arrows[x, y, z].activeSelf)) arrows[x, y, z].SetActive(true);
                    }
                    else
                    {
                        if (arrows[x, y, z].activeSelf) arrows[x, y, z].SetActive(false);
                    }

                }
    }

    public void DoTipVisualisation()
    //do things to the pointer tip according to scalar field
    {
        bool ok;

        Vector3 localtipposition = transform.InverseTransformPoint(SelectionPoint.transform.position); //find position of marker in local space of this object
        float scale = GetInterpolatedScalar(localtipposition, out ok);                                 //find scalar at that point
        //if ok is false it's outside the box 
        if (ok)
        {
            scale *= TipSizeScaleFactor;
            SelectionPoint.transform.localScale = new Vector3(scale, scale, scale);
        }
        else
        {
            SelectionPoint.transform.localScale = new Vector3(.1f, .1f, .1f);
        }

    }

    public Vector3 poscheck;
    void Update()
    //called every frame
    {
        if (StartTime < .001f) StartTime = Time.time; //This is first iteration - set StartTime

        CalcTime();

        if (LastIntTime >= TimeSteps - 1)
        //finished iterating through the data - for now we restart!
        {
            StartTime = Time.time;
            CalcTime();
        }

        OrientateArrows();  //recalculate the arrow rotations and sizes each frame

        DoTipVisualisation();

        poscheck = SelectionPoint.transform.position;
    }

    private void CalcTime()
    //work out temporary time data from current time
    {
        CurrentTime = (Time.time - StartTime) * TimeFactor;
        LastIntTime = (int)CurrentTime;
        NextIntTime = LastIntTime + 1;
        tf = CurrentTime - LastIntTime;
        omtf = 1f - tf;
    }


    public Vector3 GetInterpolatedVector(Vector3 p, out bool ok)
    //called by particles to pick their position from array using trilinear interpolation or simple sampling
    //sets 'ok' to false if particle is now outside the grid
    {
        ok = false;
        int x = (int)p.x;
        int y = (int)p.y;
        int z = (int)p.z;
        if (x < 0 || x >= XDim-1) return Vector3.zero;
        if (y < 0 || y >= YDim-1) return Vector3.zero;
        if (z < 0 || z >= ZDim-1) return Vector3.zero;

        ok = true;  //done boundary checks, will be OK from here on

        float scalefactor = ParticleVelocityScaleFactor / VectorScaleFactors[VectorIndex];    //scale velocity correctly

        if (trilinear)
        //implementation of trilinear sampling. Not properly checked, but seems to work
        {
            float xf = p.x - x;
            float yf = p.y - y;
            float zf = p.z - z;
            float omxf = 1f - xf;  //om=one minus
            float omyf = 1f - yf;
            float omzf = 1f - zf;
            //Corner vectors of sampling cube - interpolated between timeslices
            Vector3 v000 = VectorArray[x, y, z, LastIntTime, VectorIndex] * omtf + VectorArray[x, y, z, NextIntTime, VectorIndex] * tf;
            Vector3 v100 = VectorArray[x + 1, y, z, LastIntTime, VectorIndex] * omtf + VectorArray[x + 1, y, z, NextIntTime, VectorIndex] * tf;
            Vector3 v010 = VectorArray[x, y + 1, z, LastIntTime, VectorIndex] * omtf + VectorArray[x, y + 1, z, NextIntTime, VectorIndex] * tf;
            Vector3 v001 = VectorArray[x, y, z + 1, LastIntTime, VectorIndex] * omtf + VectorArray[x, y, z + 1, NextIntTime, VectorIndex] * tf;
            Vector3 v101 = VectorArray[x + 1, y, z + 1, LastIntTime, VectorIndex] * omtf + VectorArray[x + 1, y, z + 1, NextIntTime, VectorIndex] * tf;
            Vector3 v110 = VectorArray[x + 1, y + 1, z, LastIntTime, VectorIndex] * omtf + VectorArray[x + 1, y + 1, z, NextIntTime, VectorIndex] * tf;
            Vector3 v011 = VectorArray[x, y + 1, z + 1, LastIntTime, VectorIndex] * omtf + VectorArray[x, y + 1, z + 1, NextIntTime, VectorIndex] * tf;
            Vector3 v111 = VectorArray[x + 1, y + 1, z + 1, LastIntTime, VectorIndex] * omtf + VectorArray[x + 1, y + 1, z + 1, NextIntTime, VectorIndex] * tf;

            return (v000 * omxf * omyf * omzf
                + v100 * xf * omyf * omzf
                + v010 * omxf * yf * omzf
                + v001 * omxf * omyf * zf
                + v101 * xf * omyf * zf
                + v011 * omxf * yf * zf
                + v110 * xf * yf * omzf
                + v111 * xf * yf * zf)*scalefactor;
        }
        else
        {
            //directly sample array for data, only interpolating between timeslices
            return (VectorArray[x, y, z, LastIntTime, VectorIndex] * omtf + VectorArray[x, y, z, NextIntTime, VectorIndex] * tf)*scalefactor;
        }
    }


    public float GetInterpolatedScalar(Vector3 p, out bool ok)
    //Scalar version of GetInterpolatedVector
    {
        ok = false;
        int x = (int)p.x;
        int y = (int)p.y;
        int z = (int)p.z;
        if (x < 0 || x >= XDim - 1) return 1f;
        if (y < 0 || y >= YDim - 1) return 1f;
        if (z < 0 || z >= ZDim - 1) return 1f;

        ok = true;  //done boundary checks, will be OK from here on

        float scalefactor = ParticleVelocityScaleFactor / ScalarScaleFactors[VectorIndex];    //scale  correctly

        if (trilinear)
        //implementation of trilinear sampling. Not properly checked, but seems to work
        {
            float xf = p.x - x;
            float yf = p.y - y;
            float zf = p.z - z;
            float omxf = 1f - xf;  //om=one minus
            float omyf = 1f - yf;
            float omzf = 1f - zf;
            //Corner vectors of sampling cube - interpolated between timeslices
            float v000 = ScalarArray[x, y, z, LastIntTime, ScalarIndex] * omtf + ScalarArray[x, y, z, NextIntTime, ScalarIndex] * tf;
            float v100 = ScalarArray[x + 1, y, z, LastIntTime, ScalarIndex] * omtf + ScalarArray[x + 1, y, z, NextIntTime, ScalarIndex] * tf;
            float v010 = ScalarArray[x, y + 1, z, LastIntTime, ScalarIndex] * omtf + ScalarArray[x, y + 1, z, NextIntTime, ScalarIndex] * tf;
            float v001 = ScalarArray[x, y, z + 1, LastIntTime, ScalarIndex] * omtf + ScalarArray[x, y, z + 1, NextIntTime, ScalarIndex] * tf;
            float v101 = ScalarArray[x + 1, y, z + 1, LastIntTime, ScalarIndex] * omtf + ScalarArray[x + 1, y, z + 1, NextIntTime, ScalarIndex] * tf;
            float v110 = ScalarArray[x + 1, y + 1, z, LastIntTime, ScalarIndex] * omtf + ScalarArray[x + 1, y + 1, z, NextIntTime, ScalarIndex] * tf;
            float v011 = ScalarArray[x, y + 1, z + 1, LastIntTime, ScalarIndex] * omtf + ScalarArray[x, y + 1, z + 1, NextIntTime, ScalarIndex] * tf;
            float v111 = ScalarArray[x + 1, y + 1, z + 1, LastIntTime, ScalarIndex] * omtf + ScalarArray[x + 1, y + 1, z + 1, NextIntTime, ScalarIndex] * tf;

            return (v000 * omxf * omyf * omzf
                + v100 * xf * omyf * omzf
                + v010 * omxf * yf * omzf
                + v001 * omxf * omyf * zf
                + v101 * xf * omyf * zf
                + v011 * omxf * yf * zf
                + v110 * xf * yf * omzf
                + v111 * xf * yf * zf) * scalefactor;
        }
        else
        {
            //directly sample array for data, only interpolating between timeslices
            return (ScalarArray[x, y, z, LastIntTime, ScalarIndex] * omtf + ScalarArray[x, y, z, NextIntTime, ScalarIndex] * tf) * scalefactor;
        }
    }

    public void MakeParticle(bool shortlived)
    //called from Touch Controller Input script - make a particle at the selected position
    //shortlived uses a different prefab with a very short life - use for continuous indication
    {
        GameObject p;
        if (shortlived)
            p=Instantiate(PrefabParticleShortLived, transform);                         //make a particle, with this object as parent
        else
            p=Instantiate(PrefabParticle, transform);                         //make a particle, with this object as parent

        p.transform.position = SelectionPoint.transform.position;           //Set its position at the tip of the pointer...
        float scale = Mathf.Sqrt(tcs.GetScale())/16f;
        if (shortlived) scale *= 2f;
        p.transform.localScale = new Vector3(scale, scale, scale);          //And scale it according to current view scale

        ParticleCount++;                                                                        //increment particle counter    
    }

    

}
