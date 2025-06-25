using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class PortalConfigs
{
    // List of links of portals on this body
    public List<SinglePortalConfig> Portals;
}
public class SinglePortalConfig
{
    public string name = null;
    public String linkedPortal = null;
    public String sector = null;
    public int portalMaxRecursion = 0;
    public int portalMaxRenderDistance = 50;
    public int portalFarClipPlane = 1000;
}
