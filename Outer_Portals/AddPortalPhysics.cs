using NewHorizons.Utility.OuterWilds;
using System.Collections;
using UnityEngine;

namespace NewHorizons.Components;


// Taken and modified from: https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Components/AddPhysics.cs
// Needed to make custom adjustments to handle AlignmentForceDetector

/// <summary>
/// properly add physics to a detail
/// </summary>
[DisallowMultipleComponent]
public class AddPortalPhysics : MonoBehaviour
{
    [Tooltip("The sector that the rigidbody will be simulated in, or none for it to always be on.")]
    public Sector Sector;
    [Tooltip("The mass of the physics object.\n" +
        "Most pushable props use the default value, which matches the player mass.")]
    public float Mass = 0.001f;
    [Tooltip("The radius that the added sphere collider will use for physics collision.\n" +
        "If there's already good colliders on the detail, you can make this 0.")]
    public bool SuspendUntilImpact;

    private OWRigidbody _body;
    private ImpactSensor _impactSensor;

    private IEnumerator Start()
    {
        // detectors dont detect unless we wait for some reason
        yield return new WaitForSeconds(.1f);

        var parentBody = GetComponentInParent<OWRigidbody>();

        // hack: make all mesh colliders convex
        // triggers are already convex
        // doesnt work for some non readable meshes but whatever
        foreach (var meshCollider in GetComponentsInChildren<MeshCollider>(true))
            meshCollider.convex = true;

        var bodyGo = new GameObject($"{name}_Body");
        bodyGo.SetActive(false);
        bodyGo.transform.parent = transform.parent;
        bodyGo.transform.position = transform.position;
        bodyGo.transform.rotation = transform.rotation;

        _body = bodyGo.AddComponent<OWRigidbody>();
        _body._simulateInSector = Sector;

        bodyGo.layer = Layer.PhysicalDetector;
        bodyGo.tag = "DynamicPropDetector";
        // this collider is not included in groups. oh well
        bodyGo.AddComponent<SphereCollider>().radius = 0;
        var shape = bodyGo.AddComponent<SphereShape>();
        shape._collisionMode = Shape.CollisionMode.Detector;
        shape._layerMask = (int)(Shape.Layer.Default | Shape.Layer.Gravity);
        shape._radius = 0;

        _impactSensor = bodyGo.AddComponent<ImpactSensor>();
        var audioSource = bodyGo.AddComponent<AudioSource>();
        audioSource.maxDistance = 30;
        audioSource.dopplerLevel = 0;
        audioSource.rolloffMode = AudioRolloffMode.Custom;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1;
        var owAudioSource = bodyGo.AddComponent<OWAudioSource>();
        owAudioSource._audioSource = audioSource;
        owAudioSource._track = OWAudioMixer.TrackName.Environment;
        var objectImpactAudio = bodyGo.AddComponent<ObjectImpactAudio>();
        objectImpactAudio._minPitch = 0.4f;
        objectImpactAudio._maxPitch = 0.6f;
        objectImpactAudio._impactSensor = _impactSensor;

        GameObject detectorObject = new GameObject($"{name}_detector");
        detectorObject.transform.parent = bodyGo.transform;
        detectorObject.layer = Layer.BasicDetector;
        detectorObject.AddComponent<AlignmentForceDetector>();
        detectorObject.AddComponent<OWCollider>();
        var sc = detectorObject.AddComponent<SphereCollider>();
        sc.radius = 0;
        var ss = detectorObject.AddComponent<SphereShape>();
        ss.radius = 0;
        ss.SetCollisionMode(Shape.CollisionMode.Detector);
        ss.layerMask = 5;


        bodyGo.SetActive(true);

        transform.parent = bodyGo.transform;
        _body.SetMass(Mass);
        _body.SetVelocity(parentBody.GetPointVelocity(_body.GetWorldCenterOfMass()));
        _body.SetAngularVelocity(parentBody.GetAngularVelocity());

        // #536 - Physics objects in bramble dimensions not disabled on load
        // sectors wait 3 frames and then call OnSectorOccupantsUpdated
        // however we wait .1 real seconds which is longer
        // so we have to manually call this
        //if (_body._simulateInSector) _body.OnSectorOccupantsUpdated();

        // copied from OWRigidbody.Suspend
        _body.Suspend();
        RigidBody_OnUnsuspendOWRigidbody(_body);
        _body.OnUnsuspendOWRigidbody += RigidBody_OnUnsuspendOWRigidbody;
        _body._suspensionBody = parentBody;
        _body._transform.parent = parentBody.transform;
        _body._suspended = true;
        _body._unsuspendNextUpdate = false;

        if (Sector != null)
        {
            bodyGo.transform.parent = Sector.gameObject.transform;
        }

        // match velocity doesnt work so just make it not targetable
        _body.SetIsTargetable(false);

        Destroy(this);
    }
    private void RigidBody_OnUnsuspendOWRigidbody(OWRigidbody suspendedBody)
    {
        // Function for keeping the portals permenantly suspended.
        // However, we have to prevent the colliders from disabling, so we re-enable them after re-suspending the OWRigidBody.
        suspendedBody.Suspend();

        var _childColliders = suspendedBody.GetComponentsInChildren<Collider>();
        for (int i = 0; i < _childColliders.Length; i++)
        {
            _childColliders[i].gameObject.GetAddComponent<OWCollider>().OnUnsuspendOWRigidbody(suspendedBody);
        }
    }
}