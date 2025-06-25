using NewHorizons.Utility.OWML;
using OuterPortals.src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PortalActivationVolume : MonoBehaviour {

    public List<GameObject> managedPortals = new List<GameObject>();
    private OWCollider volume;
    private List<Collider> occupants = new List<Collider>();

    public void Start()
    {
        
        volume = gameObject.GetComponent<OWCollider>();
        if (volume == null)
        {
            NHLogger.LogError("PortalActivationVolume volume is null!");
            return;
        }
    }

    public void OnTriggerEnter(Collider occupant)
    {
        if (occupant.tag != "Player")
            return;
        if (occupants.Contains(occupant))
            return;
        occupants.Add(occupant);
        foreach (GameObject p in managedPortals)
        {
            if (p == null)
            {
                NHLogger.LogError("Somehow a managed portal is null");
                continue;
            }
            p.SetActive(true);
        }
    }
    public void OnTriggerExit(Collider occupant)
    {
        if (occupant.tag != "Player")
            return;
        if (!occupants.Contains(occupant))
            return;
        occupants.Remove(occupant);
        if (occupants.Count == 0) { 
            foreach (GameObject p in managedPortals)
            {
                if (p == null)
                {
                    NHLogger.LogError("Somehow a managed portal is null");
                    continue;
                }
                p.SetActive(false);
            }
        }
    }
}
