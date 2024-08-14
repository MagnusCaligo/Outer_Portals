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
using System.Drawing.Drawing2D;

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

        private static Shader portalShader;
        private static List<Camera> cameras = new List<Camera>();
        private bool lastVisibility = false;
        private VisibilityObject visibilityObject;
        private bool doTransformations = true;

        // Corners for calculating clipping
        private List<Vector3> corners;
        private Rect viewportRect;
        private int debugCount = 0;
        private static Camera playerCamera;
        private MeshRenderer meshRenderer;
        private int currentLODValue = 0;
        //private static float[] portalRectLODPercentagePoints = {0.01f, 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.35f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f, 0.65f, 0.75f, 0.80f, 0.85f, 0.90f, 0.95f, 1.0f };
        private static float[] portalRectLODPercentagePoints = {0.01f, 0.10f, 0.50f, 1.0f };

        public void Awake()
        {
            portalShader = First_Test_Mod.Instance.shaderBundle.LoadAsset<Shader>("Assets/Custom Prefabs/PortalShader.shader");
        }

        public void Start()
        {
            // Setup Corners
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);
            corners = new List<Vector3>();
            corners.Add(new Vector3(-radiusOfPortal, 0, 0));
            corners.Add(new Vector3(-radiusOfPortal, 2*radiusOfPortal, 0));
            corners.Add(new Vector3(radiusOfPortal, 0, 0));
            corners.Add(new Vector3(radiusOfPortal, 2*radiusOfPortal, 0));

            meshRenderer = gameObject.GetAddComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                NHLogger.LogError("MESH RENDER IS NULL");
            }

            if (playerCamera == null)
                playerCamera = Locator.GetPlayerCamera().mainCamera;

            if (camera == null)
                camera = gameObject.GetComponent<Camera>();
            cameras.Add(camera);
            camera.cullingMask = 4194321;
            camera.backgroundColor = Color.black;
            camera.farClipPlane = 5000;


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
        public static bool RectContainsRect(Rect parentRect, Rect childRect)
        {
            // Checks to see if child rect is within the parent
            return childRect.xMin >= parentRect.xMin
                && childRect.yMin >= parentRect.yMin
                && childRect.xMax <= parentRect.xMax
                && childRect.yMax <= parentRect.yMax;
        }

        // Made based off of this forum post: https://discussions.unity.com/t/how-do-i-render-only-a-part-of-the-cameras-view/23686/2
        public void SetScissorRect(Rect matrixRect, Rect cameraRect)
        {
            //Matrix4x4 m = Locator.GetPlayerCamera().mainCamera.projectionMatrix;
            camera.ResetProjectionMatrix();
            Matrix4x4 m = camera.projectionMatrix;
            camera.rect = cameraRect;
            camera.aspect = playerCamera.aspect;
            
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3((1 / matrixRect.width - 1), (1 / matrixRect.height - 1), 0), Quaternion.identity, new Vector3(1 / matrixRect.width, 1 / matrixRect.height, 1));
            Matrix4x4 m3 = Matrix4x4.TRS(new Vector3(-matrixRect.x * 2 / matrixRect.width, -matrixRect.y * 2 / matrixRect.height, 0), Quaternion.identity, Vector3.one);
            camera.projectionMatrix = m3 * m2 * m;

        }

        // Written based off of this article: https://www.turiyaware.com/a-solution-to-unitys-camera-worldtoscreenpoint-causing-ui-elements-to-display-when-object-is-behind-the-camera/
        public void CalculateViewportRect()
        {
            List<Vector3> points_on_screen = new List<Vector3>();
            corners.ForEach(x => points_on_screen.Add(playerCamera.WorldToScreenPoint(transform.TransformPoint(x))));
            
            Vector3 left_most_point = points_on_screen.OrderBy(x => x.x).First();
            Vector3 right_most_point = points_on_screen.OrderBy(x => x.x).Last();
            Vector3 top_most_point = points_on_screen.OrderBy(x => x.y).Last();
            Vector3 bottom_most_point = points_on_screen.OrderBy(x => x.y).First();

            Vector3 initialPoint = points_on_screen.OrderBy(x => x.z).Last();

            float xMin = initialPoint.x;
            float xMax = xMin;
            float yMin = initialPoint.y;
            float yMax = yMin;

            points_on_screen.ForEach(point =>
            {
                if (point.z <= 0)
                {

                    if (point.x <= Screen.width / 2f)
                        point.x = Screen.width;
                    else if (point.x > Screen.width / 2f)
                        point.x = 0;
                    if (point.y <= Screen.height / 2f)
                        point.y = Screen.height;
                    else if (point.y > Screen.height / 2f)
                        point.y = 0;
                }

                if (point.x < xMin)
                    xMin = point.x;
                if (point.x > xMax)
                    xMax = point.x;
                if (point.y < yMin)
                    yMin = point.y;
                if (point.y > yMax)
                    yMax = point.y;
            });

            float bottom_point_x = Mathf.Clamp(left_most_point.x / Screen.width, 0, 1);
            float bottom_point_y = Mathf.Clamp(bottom_most_point.y / Screen.height, 0, 1);
            float width_value = Mathf.Clamp((Mathf.Clamp(right_most_point.x, 0, Screen.width) - Mathf.Clamp(left_most_point.x, 0, Screen.width)) / Screen.width, 0, 1);
            float height_value = Mathf.Clamp((Mathf.Clamp(top_most_point.y, 0, Screen.height) - Mathf.Clamp(bottom_most_point.y, 0, Screen.height)) / Screen.height, 0, 1);

            xMin = Mathf.Clamp(xMin / Screen.width, 0, 1);
            xMax = Mathf.Clamp(xMax / Screen.width, 0, 1);
            yMin = Mathf.Clamp(yMin / Screen.height, 0, 1);
            yMax = Mathf.Clamp(yMax / Screen.height, 0, 1);

            Rect minimumRect= new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            Rect rect = new Rect(xMin, yMin, portalRectLODPercentagePoints[currentLODValue], portalRectLODPercentagePoints[currentLODValue]);

            // Now that we know the minimum size we need to render, find the next LOD level
            if (name == "th_to_ar" && debugCount % 10 == 0)
            {
                NHLogger.Log($"Minimum rect: {minimumRect}\nCurrentRect: {rect}");
            }
            while (minimumRect.width > rect.width || minimumRect.height > rect.height)
            {
                if (currentLODValue == portalRectLODPercentagePoints.Length - 1)
                {
                    NHLogger.LogError("Already at max rect, but need a bigger size! Something is wrong.");
                    SetScissorRect(minimumRect, rect);
                    return;
                }
                currentLODValue++;
                rect = new Rect(xMin, yMin, portalRectLODPercentagePoints[currentLODValue], portalRectLODPercentagePoints[currentLODValue]);
            }
            // Check if we can downsize the rect

            Rect smallerPossibleRect = new Rect(xMin, yMin, portalRectLODPercentagePoints[currentLODValue - 1], portalRectLODPercentagePoints[currentLODValue - 1]);
            while (minimumRect.width < smallerPossibleRect.width && minimumRect.height < smallerPossibleRect.height)
            {
                if (currentLODValue == 0)
                {
                    NHLogger.LogError("Already at smallest rect, but need a smaller size! Something is wrong.");
                    SetScissorRect(minimumRect, rect);
                    return;
                }
                currentLODValue--;
                rect = smallerPossibleRect;
                smallerPossibleRect = new Rect(xMin, yMin, portalRectLODPercentagePoints[currentLODValue - 1], portalRectLODPercentagePoints[currentLODValue - 1]);
            }
            SetScissorRect(minimumRect, rect);
        }

        // you would think this should be in FixedUpdate since it depends on player movement,
        // but doing that makes it lag one frame behind
        public void Update()
        {
            debugCount++;
            UpdateVisibility();
            if (!doTransformations)
                return;
            
            /*
            if (!meshRenderer.isVisible)
            {
                camera.enabled = false;
                return;
            }
            */
            if (name == "th_to_ar" && debugCount % 10 == 0)
            {
                NHLogger.Log($"Portal LOD is {currentLODValue}");
            }
            
            
            camera.enabled = true;

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
            if (linkedToSelf)
                newPos += - 0.1f * output_portal_transform.forward;

            outputPortalUp = output_portal_transform.rotation * Vector3.up;
            newRot = Quaternion.LookRotation(-output_portal_transform.forward, outputPortalUp) * transform.InverseTransformRotation(playerCamera.transform.rotation);
            
            // Calculate clip distance to maximize camera through portal while minimizing rendering stuff between camera and portal
            Plane clip = new Plane(camera.transform.forward, camera.transform.position);

            // Find Closest Corner
            Vector3 closestCorner = corners.OrderBy(x => clip.GetDistanceToPoint(output_portal_transform.TransformPoint(x))).First();
            closestCorner = output_portal_transform.TransformPoint(closestCorner);

            // Shift plane to be in line with corner
            clip = new Plane(camera.transform.forward, closestCorner);

            // Calculate distance between camera and plane
            float closestDistance = -clip.GetDistanceToPoint(camera.transform.position);
            closestDistance = closestDistance < 0.1f ? 0.1f : closestDistance;

            camera.transform.position = newPos;
            camera.transform.rotation = newRot;
            camera.nearClipPlane = closestDistance;
            CalculateViewportRect();
        }

        public void OnVisible()
        {
            camera.enabled = true;
            doTransformations = true;

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
        }
        
        public void OnInvisible()
        {
            camera.enabled = false;
            doTransformations = false;
            var linkedPortalSectorName = linkedPortal.sectorName;
            if (linkedPortalSectorName == null)
                return;
            Sector sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name.Equals(linkedPortalSectorName));
            if (sector == null)
                return;
            do
            {
                sector.RemoveOccupant(Locator.GetPlayerSectorDetector());
                sector = sector.GetParentSector();
            } while (sector != null);
        }

        public void UpdateVisibility()
        {
            if (visibilityObject != null)
            {
                if (linkedPortal == null)
                    return;
                if (visibilityObject.IsVisible() && !lastVisibility)
                {
                    OnVisible();
                }else if(!visibilityObject.IsVisible() && lastVisibility)
                {
                    OnInvisible();
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