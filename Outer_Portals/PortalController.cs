using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using NewHorizons.Utility.OWML;
using NewHorizons.Handlers;
using NewHorizons.Components;
using UnityEngine.PostProcessing;

namespace OuterPortals.src
{
    // see https://github.com/TerrificTrifid/ow-nh-quasar-project/blob/main/QuasarProject/PortalController.cs as well
    [HarmonyPatch]
    public class PortalController : MonoBehaviour
    {

        public static int maximumRenderDistance = 100;  // This is how close you need to be to a portal before it will actually start rendering. Used for performance.

        public Camera camera;
        public GameObject renderPlane;
        public PortalController linkedPortal;
        public bool linkedToSelf = false;
        public String sectorName;
        public GameObject teleportationPlane;
        public GameObject PortalSectorDetector;

        private static readonly List<Camera> cameras = new List<Camera>();
        private static List<PortalController> portalControllers = new List<PortalController>();
        private bool lastVisibility = false;
        private VisibilityObject visibilityObject;
        private bool doTransformations = true;
        private List<OWRigidbody> teleportationOccupants;
        private SectorDetector sectorDetector;
        private OWCamera owCamera;
        private static RenderTexture oldTexture = null;

        // Corners for calculating clipping
        private List<Vector3> corners;
        private static Camera playerCamera;
        
        private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

        private Material cameraMaterial = new Material(OuterPortals.portalShader);
        private RenderTexture cameraRenderTexture = new RenderTexture(Screen.width, Screen.height, 24);

        public void Start()
        {

            var sector = SectorManager.GetRegisteredSectors().Find(sector => sector.name == transform.parent.name);
            Locator.GetPlayerCamera().GetComponentInParent<PlanetaryFogImageEffect>().enabled = false;


            // Setup Corners
            float radiusOfPortal = transform.localScale.x * (renderPlane.transform.localScale.x / 2f);
            corners = new List<Vector3>();
            corners.Add(new Vector3(-radiusOfPortal, 0, 0));
            corners.Add(new Vector3(-radiusOfPortal, 2 * radiusOfPortal, 0));
            corners.Add(new Vector3(radiusOfPortal, 0, 0));
            corners.Add(new Vector3(radiusOfPortal, 2 * radiusOfPortal, 0));

            if (playerCamera == null)
                playerCamera = Locator.GetPlayerCamera().mainCamera;

            cameras.Add(camera);
            camera.targetTexture = cameraRenderTexture;
            cameraMaterial.mainTexture = cameraRenderTexture;
            renderPlane.GetComponent<MeshRenderer>().sharedMaterial = cameraMaterial;
            portalControllers.Add(this);

            visibilityObject = renderPlane.GetAddComponent<VisibilityObject>();
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

            owCamera = gameObject.GetComponentsInChildren<OWCamera>()[0];
            owCamera._postProcessingSettings = Locator.GetPlayerCamera().postProcessingSettings;
            Locator.GetPlayerBody().GetComponentInChildren<OWCamera>().onThisPreRender.AddListener((OWCamera) => this.checkVisibilityOfPortalsFromPlayerCamera());
            camera.enabled = false;
            owCamera.enabled = false;
            // Start invisible
            OnInvisible();
        }

        public void onEntryTeleporationPlane(GameObject obj)
        {
            OWCollider component = obj.GetComponent<OWCollider>();
            if (component.CompareTag("PlayerDetector") || component.CompareTag("ProbeDetector"))
            {
                var body = obj.GetComponentInParent<OWRigidbody>();
                teleportationOccupants.Add(body);
            }
        }

        public VisibilityObject GetVisibilityObject()
        {
            return visibilityObject;
        }

        public void onLeaveTeleportationPlane(GameObject obj)
        {
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

                if (teleportationOccupants[i].CompareTag("Player")){
                }

                var occupant = teleportationOccupants[i];
                var direction = Vector3.zero;
                direction = occupant.transform.GetAttachedOWRigidbody().GetVelocity() - transform.GetAttachedOWRigidbody().GetVelocity();

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

                    var relVel = Vector3.zero;
                    relVel = transform.ToRelVel(occupant.GetVelocity(), oldPos);

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

                    if (occupant.CompareTag("Player"))
                    {
                        var fa = Locator.GetPlayerBody().GetComponent<ForceApplier>();
                        if (fa != null)
                            fa.SkipNextFrame();
                        Locator.GetPlayerBody().GetComponent<AlignPlayerWithForce>().SkipNextFrame();
                    }
                    Vector3 scaleChange = occupant.transform.localScale - (transform.localScale - linkedPortalTransform.localScale);
                    occupant.transform.localScale = scaleChange;
                }
            }
        }

        // Made based off of this forum post: https://discussions.unity.com/t/how-do-i-render-only-a-part-of-the-cameras-view/23686/2
        public void SetScissorRect(Camera cam, Rect r)
        {
            //Matrix4x4 m = Locator.GetPlayerCamera().mainCamera.projectionMatrix;
            cam.ResetProjectionMatrix();
            Matrix4x4 m = cam.projectionMatrix;
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

        public void calculateRelativeCamera(Vector3 camera_position, Quaternion camera_rotation, Transform portal, out Vector3 out_camera_position, out Quaternion out_camera_rotation)
        {
            // Calculates where the camera should be on the otherside of the portal

            // apply transformation based on player camera
            {
                var relPos = transform.ToRelPos(camera_position);
                var relRot = transform.ToRelRot(camera_rotation);

                out_camera_position = portal.FromRelPos(halfTurn * relPos);
                out_camera_rotation = portal.FromRelRot(halfTurn * relRot);
            }
        }

        public void doMoveCameraRelativeToPosition(Vector3 camera_position, Quaternion camera_rotation, Transform output_portal)
        {
            // Adjust teleportation plane depending on direction player is facing
            // If the player is facing backwards, we need to move the teleportation back a little bit to prevent the camera from clipping through the portal
            float adjustment = (-Vector3.Dot(camera_rotation * Vector3.forward, transform.forward) + 1f) / 2f;
            teleportationPlane.transform.SetLocalPositionZ(adjustment);

            Vector3 new_position;
            Quaternion new_rotation;
            calculateRelativeCamera(camera_position, camera_rotation, output_portal, out new_position, out new_rotation);
            camera.transform.position = new_position;
            camera.transform.rotation = new_rotation;

            calculateClipPlane(output_portal);
        }

        public void calculateClipPlane(Transform output_portal)
        {
            // Calculate clip distance to maximize camera through portal while minimizing rendering stuff between camera and portal
            Plane clip = new Plane(camera.transform.forward, camera.transform.position);

            // Find Closest Corner
            Vector3 closestCorner = corners.OrderBy(x => clip.GetDistanceToPoint(output_portal.TransformPoint(x))).First();
            closestCorner = output_portal.TransformPoint(closestCorner);

            // Shift plane to be in line with corner
            clip = new Plane(camera.transform.forward, closestCorner);

            // Calculate distance between camera and plane
            float closestDistance = -clip.GetDistanceToPoint(camera.transform.position);
            closestDistance = closestDistance < 0.1f ? 0.1f : closestDistance;

            camera.nearClipPlane = closestDistance;
        }
        

        public void checkVisibilityOfOtherPortals(int ttl)
        {
            if (ttl - 1 < 0)
                return;
            if (linkedToSelf) return;
            foreach (PortalController pc in portalControllers)
            {
                if (pc.enabled == false ) continue;
                if (linkedPortal == null || pc == linkedPortal) continue;
                var distance = (linkedPortal.transform.position - pc.transform.position).magnitude;
                if (distance > maximumRenderDistance)
                    continue;
                var visible = pc.GetVisibilityObject().CheckVisibilityFromProbe(owCamera);
                if (visible && Vector3.Dot(transform.position - pc.transform.position, pc.transform.forward) <= 0f)
                {
                    PortalController linkedPortal = pc.linkedPortal;
                    if (pc.linkedPortal == null)
                        linkedPortal = pc;

                    Vector3 camera_location = Vector3.zero;
                    Quaternion camera_rotation = Quaternion.identity;
                    Vector3 oldCameraLocation = pc.camera.transform.position;
                    Quaternion oldCameraRotation = pc.camera.transform.rotation;
                    pc.OnVisible();
                    pc.doMoveCameraRelativeToPosition(camera.transform.position, camera.transform.rotation, linkedPortal.transform);
                    pc.checkVisibilityOfOtherPortals(ttl - 1);
                    pc.calculateClipPlane(linkedPortal.transform);
                    pc.owCamera.Render();
                    pc.camera.transform.position = oldCameraLocation;
                    pc.camera.transform.rotation = oldCameraRotation;

                    continue;
                }
                pc.OnInvisible();
            }

        }

        public void checkVisibilityOfPortalsFromPlayerCamera()
        {
            foreach (PortalController pc in portalControllers)
            {
                var distance = (playerCamera.transform.position - pc.transform.position).magnitude; 
                if (distance > maximumRenderDistance) continue;
                if (pc.GetVisibilityObject().IsVisible() && Vector3.Dot(playerCamera.transform.position - pc.transform.position, pc.transform.forward) < 0f)
                {
                    Transform output_portal_transform;
                    if (pc.linkedToSelf || pc.linkedPortal == null)
                    {
                        output_portal_transform = pc.transform;
                    }
                    else
                    {
                        output_portal_transform = pc.linkedPortal.transform;
                    }

                    pc.checkVisibilityOfOtherPortals(2);
                    pc.OnVisible();
                    pc.doMoveCameraRelativeToPosition(playerCamera.transform.position, playerCamera.transform.rotation, output_portal_transform);
                    pc.owCamera.Render();
                    continue;
                }
                pc.OnInvisible();
            }

        }

        public void OnVisible()
        {
            if (lastVisibility)
                return;
            lastVisibility = true;
            doTransformations = true;

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
            if (lastVisibility == false)
                return;
            lastVisibility = false;

            doTransformations = false;

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
                OnVisible();
                lastVisibility = true;
            }
            else if (lastVisibility && (!visibilityObject.IsVisible()
                || !playerInSector
                || positionDifference.magnitude >= maximumRenderDistance))
            {
                OnInvisible();
                lastVisibility = false;
            }
        }

        public void OnDestroy()
        {
            cameras.Remove(camera);
            portalControllers.Remove(this);
            DestroyImmediate(cameraRenderTexture);
            DestroyImmediate(cameraMaterial);
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
                    OuterPortals.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Key}");
                    continue;
                }
                if (exit_portal == null)
                {
                    OuterPortals.Instance.ModHelper.Console.WriteLine($"Error: Failed to find portal with name {link.Value}");
                    continue;
                }
                PortalController entr_portal_controller = entrance_portal.GetComponent<PortalController>();
                PortalController exit_portal_controller = exit_portal.GetComponent<PortalController> ();
                if (entr_portal_controller == null)
                {
                    OuterPortals.Instance.ModHelper.Console.WriteLine($"Entrance portal with name {link.Key} does not have a portal controller.");
                    continue;
                }
                if (exit_portal_controller == null)
                {
                    OuterPortals.Instance.ModHelper.Console.WriteLine($"Exit portal with name {link.Value} does not have a portal controller.");
                    continue;
                }
                OuterPortals.Instance.ModHelper.Console.WriteLine($"Linking {link.Key} to {link.Value}");
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