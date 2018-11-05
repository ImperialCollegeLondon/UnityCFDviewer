using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class statics {
    static public CFDObjectScript cfd = null;

    public static CFDObjectScript getcfd()
    {
        if (cfd == null)
            cfd = GameObject.Find("CFDobject").GetComponent<CFDObjectScript>();
        return cfd;
    }
}
