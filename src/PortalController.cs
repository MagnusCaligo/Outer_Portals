using OWML.ModHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using UnityEngine.Windows.WebCam;
using NewHorizons.Components;
using NewHorizons.Utility.OWML;
using HarmonyLib.Tools;
using NewHorizons.Utility;

namespace First_Test_Mod.src
{
    [HarmonyPatch]
    internal class PortalController : MonoBehaviour
    {

        public Camera camera;
        public GameObject renderPlane;
        private Material cameraMaterial;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;
        public String sectorName;

        //private SectorDetector sectorDetector;

        public static OWCamera playerCamera;
        private static Shader portalShader;
        private static List<Camera> cameras = new List<Camera>();
        private Renderer renderer;
        private bool lastVisibility = false;
        private VisibilityObject visibilityObject;
        private Plane plane;

        private GameObject debugSphere;
        private GameObject debugPlane;
        private bool useDebugSphere;
        private int counter = 0;
        private List<Vector3> corners;


        public void Awake()
        {
            portalShader = First_Test_Mod.Instance.shaderBundle.LoadAsset<Shader>("Assets/Custom Prefabs/PortalShader.shader");
            
        }

        public void Start()
        {
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);
            NHLogger.LogError($"Radius is: {radiusOfPortal}");
            corners = new List<Vector3>();
            corners.Add(new Vector3(-radiusOfPortal, -radiusOfPortal, 0));
            corners.Add(new Vector3(-radiusOfPortal, radiusOfPortal, 0));
            corners.Add(new Vector3(radiusOfPortal, -radiusOfPortal, 0));
            corners.Add(new Vector3(radiusOfPortal, radiusOfPortal, 0));
            corners.ForEach(x =>
            {
                NHLogger.LogError($"Corner is {x}");
            });


            if (name == "portal1")
            {
                debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                debugSphere.DestroyAllComponents<Collider>();
                debugSphere.transform.parent = transform;
                debugSphere.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
                useDebugSphere = true;

                /*
                debugPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debugPlane.DestroyAllComponents<Collider>();
                debugPlane.transform.parent = transform;
                debugPlane.transform.localScale = new Vector3(3f, 3f, 0.1f);
                */
            }

            plane = new Plane(-renderPlane.transform.forward, renderPlane.transform.position);
            if (playerCamera == null)
                playerCamera = Locator.GetActiveCamera();

            if (camera == null)
                camera = gameObject.GetComponent<Camera>();
            cameras.Add(camera);
            camera.cullingMask = 4194321;
            camera.backgroundColor = Color.black;
            //camera.projectionMatrix = Matrix4x4.Perspective(70f, Screen.width/Screen.height, 1, 100) * Matrix4x4.Rotate(Quaternion.Euler(0, -15, 0));
            //camera.transform.rotation *= Quaternion.EulerRotation(0, 45, 0);

            if (portalShader == null)
            {
                Debug.LogError("Shader not found! Setting to empty one.");
                portalShader = Shader.Find("Unlit/Color");
            }
            cameraMaterial = new Material(portalShader);

            if (camera.targetTexture != null)
                camera.targetTexture.Release();
            camera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            cameraMaterial.mainTexture = camera.targetTexture;

            renderPlane.GetComponent<MeshRenderer>().material = cameraMaterial;
            visibilityObject = renderPlane.GetComponent<VisibilityObject>();

        }

        public void FixedUpdate()
        {
            Vector3 newPos;
            Quaternion newRot;
            Vector3 playerInLocal;
            Vector3 outputPortalUp;
            Transform output_portal_transform;
            if (linkedToSelf)
            {
                output_portal_transform = transform;
            }
            else
            {
                if (linkedPortal == null)
                    return;
                output_portal_transform = linkedPortal.transform;
            }

            playerInLocal = transform.InverseTransformPoint(playerCamera.transform.position);
            playerInLocal = new Vector3(-playerInLocal.x, playerInLocal.y, -playerInLocal.z);  // Position on opposite side of portal
            newPos = output_portal_transform.TransformPoint(playerInLocal);

            outputPortalUp = output_portal_transform.rotation * Vector3.up;
            newRot = Quaternion.LookRotation(-output_portal_transform.forward, outputPortalUp) * transform.InverseTransformRotation(playerCamera.transform.rotation);


            
            // Calculate clip distance to maximize camera through portal while minimizing rendering stuff between camera and portal
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);

            Plane clip = new Plane(camera.transform.forward, camera.transform.position);

            // Find Closest Corner
            Vector3 closestCorner = corners.OrderBy(x => clip.GetDistanceToPoint(output_portal_transform.TransformPoint(x))).First();
            closestCorner = output_portal_transform.TransformPoint(closestCorner);
            //closestCorner = new Vector3(radiusOfPortal, radiusOfPortal, 0);

            // Shift plane to be in line with corner
            //clip.distance -= clip.GetDistanceToPoint(closestCorner);
            clip = new Plane(camera.transform.forward, closestCorner);

            // Calculate distance between camera and plane
            float closestDistance = -clip.GetDistanceToPoint(camera.transform.position);
            closestDistance = closestDistance < 0.1f ? 0.1f : closestDistance;
            

            if (useDebugSphere)
            {
                if (counter % 10 == 0)
                {

                    //NHLogger.Log($"Distance: {closestDistance}\nClosest Corner: {output_portal_transform.InverseTransformPoint(closestCorner)}");
                    foreach (Vector3 corner in corners)
                        NHLogger.Log($"Corner is {corner}");
                }
                counter++;
               debugSphere.transform.position = closestCorner;
            }

            camera.transform.position = newPos;
            camera.transform.rotation = newRot;
            camera.nearClipPlane = closestDistance;

            UpdateVisibility();

        }

        public void UpdateVisibility()
        {
            if (visibilityObject != null)
            {
                if (linkedPortal == null)
                    return;
                if (visibilityObject.IsVisible() && !lastVisibility)
                {
                    var linkedPortalSectorName = linkedPortal.sectorName;
                    if (linkedPortalSectorName == null)
                        return;
                    Sector sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name.Equals(linkedPortalSectorName));
                    if (sector == null)
                        return;

                    // If the player is already there, no need to keep going
                    if (sector.ContainsOccupant(DynamicOccupant.Player))
                        return;
                    

                    do
                    {
                        NHLogger.Log($"From portal: {name}, adding to sector {sector.name}");
                        sector.AddOccupant(Locator.GetPlayerSectorDetector());
                        sector = sector.GetParentSector();
                    } while (sector != null);
                    
                }else if(!visibilityObject.IsVisible() && lastVisibility)
                {
                    var linkedPortalSectorName = linkedPortal.sectorName;
                    if (linkedPortalSectorName == null)
                        return;
                    Sector sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name.Equals(linkedPortalSectorName));
                    if (sector == null)
                        return;
                    do
                    {
                        //sector.RemoveOccupant(Locator.GetPlayerSectorDetector());
                        sector = sector.GetParentSector();
                    } while (sector != null); 
                }
                lastVisibility = visibilityObject.IsVisible();
            }
        }

        public void OnDestroy()
        {
            cameras.Remove(camera);
        }

        public static void linkPortals(PortalLinks links)
        {
            foreach (KeyValuePair<string, string> link in links.links)
            {
                GameObject entrance_portal = GameObject.Find(link.Key);
                GameObject exit_portal = GameObject.Find(link.Value);
                if (entrance_portal == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Key}");
                    continue;
                }
                if (exit_portal == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Value}");
                    continue;
                }
                PortalController entr_portal_controller = entrance_portal.GetComponent<PortalController>();
                PortalController exit_portal_controller = exit_portal.GetComponent<PortalController> ();
                if (entr_portal_controller == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Entrance portal with name {link.Key} does not have a portal controller.");
                    continue;
                }
                if (exit_portal_controller == null)
                {
                    First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Exit portal with name {link.Value} does not have a portal controller.");
                    continue;
                }
                First_Test_Mod.Instance.ModHelper.Console.WriteLine($"Linking {link.Key} to {link.Value}");
                entr_portal_controller.linkedPortal = exit_portal_controller;
                entr_portal_controller.linkedToSelf = false;
            }

            foreach (KeyValuePair<string, string> portal_and_sector in links.sectors)
            {
                GameObject portal = GameObject.Find(portal_and_sector.Key);
                if (portal == null)
                    continue;
                GameObject.Find(portal_and_sector.Key).GetComponent<PortalController>().sectorName = portal_and_sector.Value;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.UpdateFieldOfView))]
        public static void matchFieldOfView()
        {
            if (playerCamera == null)
                return;
            foreach (Camera cam in PortalController.cameras)
                cam.fieldOfView = playerCamera.fieldOfView;
        }

    }
}