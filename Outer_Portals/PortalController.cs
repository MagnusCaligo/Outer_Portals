﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using NewHorizons.Utility.OWML;
using NewHorizons.Handlers;

namespace First_Test_Mod.src
{
    // see https://github.com/TerrificTrifid/ow-nh-quasar-project/blob/main/QuasarProject/PortalController.cs as well
    [HarmonyPatch]
    internal class PortalController : MonoBehaviour
    {

        public static int maximumRenderDistance = 1000;  // This is how close you need to be to a portal before it will actually start rendering. Used for performance.

        public Camera camera;
        public GameObject renderPlane;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;
        public String sectorName;
        public GameObject teleportationPlane;
        public GameObject PortalSectorDetector;

        private static readonly List<Camera> cameras = new List<Camera>();
        private bool lastVisibility = false;
        private VisibilityObject visibilityObject;
        private bool doTransformations = true;
        private List<OWRigidbody> teleportationOccupants;
        private SectorDetector sectorDetector;

        // Corners for calculating clipping
        private List<Vector3> corners;
        private static Camera playerCamera;
        
        private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

        public void Start()
        {
            // Setup Corners
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);
            corners = new List<Vector3>();
            corners.Add(new Vector3(-radiusOfPortal, 0, 0));
            corners.Add(new Vector3(-radiusOfPortal, 2*radiusOfPortal, 0));
            corners.Add(new Vector3(radiusOfPortal, 0, 0));
            corners.Add(new Vector3(radiusOfPortal, 2*radiusOfPortal, 0));

            if (playerCamera == null)
                playerCamera = Locator.GetPlayerCamera().mainCamera;

            cameras.Add(camera);
            // TODO: do camera post processing properly, use nh Layer class for mask 
            camera.cullingMask = 4194321;
            camera.backgroundColor = Color.black;
            camera.farClipPlane = 50000;



            visibilityObject = renderPlane.GetComponent<VisibilityObject>();
            teleportationOccupants = new List<OWRigidbody>();

            if (sectorDetector == null) { 
                sectorDetector = PortalSectorDetector.GetComponent<SectorDetector>();
            }

            var triggerVolume = teleportationPlane.GetComponent<OWTriggerVolume>();
            if (triggerVolume != null)
            {
                triggerVolume.OnEntry += onEntryTeleporationPlane;
                triggerVolume.OnExit += onLeaveTeleportationPlane;
            }

        }

        public void onEntryTeleporationPlane(GameObject obj)
        {
            NHLogger.Log("Entered trigger volume");
            OWCollider component = obj.GetComponent<OWCollider>();
            if (component.CompareTag("PlayerDetector") || component.CompareTag("ProbeDetector"))
            {
                var body = obj.GetComponentInParent<OWRigidbody>();
                teleportationOccupants.Add(body);
            }
        }

        public void onLeaveTeleportationPlane(GameObject obj)
        {
            NHLogger.Log("Leave Trigger Volume");
            OWCollider component = obj.GetComponent<OWCollider>();
            if (component.CompareTag("PlayerDetector") || component.CompareTag("ProbeDetector"))
            {
                var body = obj.GetComponentInParent<OWRigidbody>();
                if (teleportationOccupants.Contains(body))
                    teleportationOccupants.Remove(body);
            }
        }

        public void UpdateTeleportOccupants()
        {

            // iterate backwards since we remove
            for (var i = teleportationOccupants.Count - 1; i >= 0; i--)
            {
                var occupant = teleportationOccupants[i];
                Vector3 direction = occupant.transform.GetAttachedOWRigidbody().GetVelocity() - transform.GetAttachedOWRigidbody().GetVelocity();
                if (Vector3.Dot(teleportationPlane.transform.up, direction) < 0f)
                {
                    Quaternion rotationDifference;
                    Transform linkedPortalTransform;
                    if (linkedToSelf || linkedPortal == null)
                    {
                        linkedPortalTransform = transform;
                    }
                    else
                    {
                        rotationDifference = transform.rotation * Quaternion.Inverse(linkedPortal.transform.rotation) * Quaternion.AngleAxis(180, linkedPortal.transform.up);
                        linkedPortalTransform = linkedPortal.transform;
                    }
                    var oldPos = occupant.GetPosition();
                    var relPos = transform.ToRelPos(oldPos);
                    var relRot = transform.ToRelRot(occupant.GetRotation());
                    var relVel = transform.ToRelVel(occupant.GetVelocity(), oldPos);
                    var relAngVel = transform.ToRelAngVel(occupant.GetAngularVelocity());

                    var newPos = linkedPortalTransform.FromRelPos(halfTurn * relPos);
                    occupant.SetPosition(newPos);
                    occupant.SetRotation(linkedPortalTransform.FromRelRot(halfTurn * relRot));
                    occupant.SetVelocity(linkedPortalTransform.FromRelVel(halfTurn * relVel, newPos));
                    occupant.SetAngularVelocity(linkedPortalTransform.FromRelAngVel(halfTurn * relAngVel));

                    if (!Physics.autoSyncTransforms) Physics.SyncTransforms(); // or else "Player grounded spherecast" complains
                    
                    if (linkedToSelf)
                    {
                        teleportationOccupants.RemoveAt(i);
                    }

                }
            }
        }

        // Made based off of this forum post: https://discussions.unity.com/t/how-do-i-render-only-a-part-of-the-cameras-view/23686/2
        public void SetScissorRect(Camera cam, Rect r)
        {
            //Matrix4x4 m = Locator.GetPlayerCamera().mainCamera.projectionMatrix;
            cam.ResetProjectionMatrix();
            Matrix4x4 m = cam.projectionMatrix;
            // if (cam.rect.size != r.size) NHLogger.Log($"changing {this} rect from {cam.rect} to {r}"); 
            cam.rect = r;
            cam.aspect = playerCamera.aspect; // does this need to be set here?
            
            Matrix4x4 m2 = Matrix4x4.TRS(new Vector3((1 / r.width - 1), (1 / r.height - 1), 0), Quaternion.identity, new Vector3(1 / r.width, 1 / r.height, 1));
            Matrix4x4 m3 = Matrix4x4.TRS(new Vector3(-r.x * 2 / r.width, -r.y * 2 / r.height, 0), Quaternion.identity, Vector3.one);
            cam.projectionMatrix = m3 * m2 * m;

        }

        // Written based off of this article: https://www.turiyaware.com/a-solution-to-unitys-camera-worldtoscreenpoint-causing-ui-elements-to-display-when-object-is-behind-the-camera/
        public void CalculateViewportRect()
        {
            return; // TEMP: setting rect lags the game a ton
            
            List<Vector3> points_on_screen = new List<Vector3>();
            corners.ForEach(x => points_on_screen.Add(playerCamera.WorldToScreenPoint(transform.TransformPoint(x))));

            Vector3 initialPoint = points_on_screen.OrderBy(x => x.z).Last();

            float xMin = initialPoint.x;
            float xMax = xMin;
            float yMin = initialPoint.y;
            float yMax = yMin;

            points_on_screen.ForEach(point =>
            {
                // if behind the camera, they coords get flipped around, so fix that here
                // it doesnt work all the time, but it does most of the time
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

            xMin = Mathf.Clamp(xMin / Screen.width, 0, 1);
            xMax = Mathf.Clamp(xMax / Screen.width, 0, 1);
            yMin = Mathf.Clamp(yMin / Screen.height, 0, 1);
            yMax = Mathf.Clamp(yMax / Screen.height, 0, 1);

            Rect rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

            SetScissorRect(camera, rect);
        }

        public void FixedUpdate()
        {
            UpdateTeleportOccupants();
        }

        // you would think this should be in FixedUpdate since it depends on player movement,
        // but doing that makes it lag one frame behind
        public void Update()
        {
            UpdateVisibility();
            if (!doTransformations)
                return;

            Transform output_portal_transform;
            if (linkedToSelf || linkedPortal == null)
            {
                output_portal_transform = transform;
            }
            else
            {
                output_portal_transform = linkedPortal.transform;
            }

            // Adjust teleportation plane depending on direction player is facing
            // If the player is facing backwards, we need to move the teleportation back a little bit to prevent the camera from clipping through the portal
            float adjustment = (-Vector3.Dot(playerCamera.transform.forward, transform.forward) + 1f) / 2f;
            teleportationPlane.transform.SetLocalPositionZ(adjustment);


            // apply transformation based on player camera
            {
                var relPos = transform.ToRelPos(playerCamera.transform.position);
                var relRot = transform.ToRelRot(playerCamera.transform.rotation);

                camera.transform.position = output_portal_transform.FromRelPos(halfTurn * relPos);
                camera.transform.rotation = output_portal_transform.FromRelRot(halfTurn * relRot);
            }

            // Calculate clip distance to maximize camera through portal while minimizing rendering stuff between camera and portal
            {
                Plane clip = new Plane(camera.transform.forward, camera.transform.position);

                // Find Closest Corner
                Vector3 closestCorner = corners.OrderBy(x => clip.GetDistanceToPoint(output_portal_transform.TransformPoint(x))).First();
                closestCorner = output_portal_transform.TransformPoint(closestCorner);

                // Shift plane to be in line with corner
                clip = new Plane(camera.transform.forward, closestCorner);

                // Calculate distance between camera and plane
                float closestDistance = -clip.GetDistanceToPoint(camera.transform.position);
                closestDistance = closestDistance < 0.1f ? 0.1f : closestDistance;

                camera.nearClipPlane = closestDistance;
            }
            
            CalculateViewportRect();
        }

        public void OnVisible()
        {
            camera.enabled = true;
            doTransformations = true;

            {
                var cameraMaterial = new Material(First_Test_Mod.portalShader);

                camera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
                cameraMaterial.mainTexture = camera.targetTexture;

                renderPlane.GetComponent<MeshRenderer>().sharedMaterial = cameraMaterial;
            }

            if (linkedPortal == null)
                return;
            
            var linkedPortalSectorName = linkedPortal.sectorName;
            if (linkedPortalSectorName == null)
                return;
            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);
            AstroObject astroObject = null;

            // Find the astro object of the sector
            while (astroObject == null && sector != null)
            {
                // some of SectorStreaming is distance based, so have to do it ourselves
                astroObject = sector.GetComponentInParent<AstroObject>();
                if (astroObject == null)
                {
                    sector = sector.GetParentSector();
                    continue;
                }
                var streamingGroup = StreamingHandler.GetStreamingGroup(astroObject.GetAstroObjectName());
                if (streamingGroup != null)
                {
                    streamingGroup.RequestRequiredAssets();
                    break;
                }
            }

            sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);

            // the same strategy NomaiRemoteCameraPlatform uses
            while (sector != null)
            {
                sector.AddOccupant(sectorDetector);
                sector = sector.GetParentSector();
            }
        }
        
        public void OnInvisible()
        {
            camera.enabled = false;
            doTransformations = false;

            {
                var cameraMaterial = renderPlane.GetComponent<MeshRenderer>().sharedMaterial;
                var renderTexture = camera.targetTexture;

                camera.targetTexture = null;
                cameraMaterial.mainTexture = null;
                renderPlane.GetComponent<MeshRenderer>().sharedMaterial = null;

                renderTexture.Release();
                DestroyImmediate(renderTexture);
                DestroyImmediate(cameraMaterial);
            }

            if (linkedPortal == null)
                return;
            
            var linkedPortalSectorName = linkedPortal.sectorName;
            if (linkedPortalSectorName == null)
                return;
            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);
            AstroObject astroObject = null;

            // Find the astro object of the sector
            while (astroObject == null && sector != null)
            {
                // some of SectorStreaming is distance based, so have to do it ourselves
                astroObject = sector.GetComponentInParent<AstroObject>();
                if (astroObject == null)
                {
                    sector = sector.GetParentSector();
                    continue;
                }
                var streamingGroup = StreamingHandler.GetStreamingGroup(astroObject.GetAstroObjectName());
                if (streamingGroup != null)
                {
                    streamingGroup.ReleaseRequiredAssets();
                    break;
                }
            }

            sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == linkedPortalSectorName);

            while (sector != null)
            {
                sector.RemoveOccupant(sectorDetector);
                sector = sector.GetParentSector();
            }
        }

        public void UpdateVisibility()
        {

            // Check that we are facing the correct way and close enough
            Vector3 positionDifference = playerCamera.transform.position - transform.position;

            // Check if the player is in the same sector as the portal
            bool playerInSector = false;
            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == sectorName);
            if (sector != null)
                playerInSector = sector.GetOccupants().Find(occupant => occupant == Locator.GetPlayerSectorDetector());
            else
                playerInSector = true;  // If the portal doesn't exist in a sector, just used the maximumRenderDistance

            if (!lastVisibility && visibilityObject.IsVisible()
                && playerInSector
                && positionDifference.magnitude < maximumRenderDistance)
            {
                NHLogger.Log($"{this} visible");
                OnVisible();
                lastVisibility = true;
            }
            else if (lastVisibility && (!visibilityObject.IsVisible()
                || !playerInSector
                || positionDifference.magnitude >= maximumRenderDistance))
            {
                NHLogger.Log($"{this} invisible");
                OnInvisible();
                lastVisibility = false;
            }
        }

        public void OnDestroy()
        {
            cameras.Remove(camera);
            OnInvisible(); // to deallocate
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

        // TODO: this can probably be moved into Update
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