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
    private OWTriggerVolume volume;
    private List<OWCollider> occupants = new List<OWCollider>();

    public void Start()
    {
        
        volume = gameObject.GetComponent<OWTriggerVolume>();
        if (volume == null)
        {
            NHLogger.LogError("PortalActivationVolume volume is null!");
            return;
        }
        volume.OnEntry += OnTriggerEnter;
        volume.OnExit += OnTriggerExit;
    }

    public void OnTriggerEnter(GameObject occupantGameObject)
    {
        
        OWCollider occupant = occupantGameObject.GetComponent<OWCollider>();
        if (occupant.tag != "PlayerDetector")
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
    public void OnTriggerExit(GameObject occupantGameObject)
    {
        OWCollider occupant = occupantGameObject.GetComponent<OWCollider>();
        if (occupant.tag != "PlayerDetector")
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
