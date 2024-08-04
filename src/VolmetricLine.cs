using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using static ShapeUtil;
using static UnityEngine.UI.Image;

[ExecuteInEditMode]
public class VolumetricLine : MonoBehaviour
{

    public float width = 5f;
    public Vector3[] vertices;
    public GameObject childrenComponents;
    public GameObject childrenObjects;
    public bool initialized = false;

    // Start is called before the first frame update
    void Start()
    {
    }

    void makeChildrenComponents()
    {
        Debug.Log("Making Object");
        childrenComponents = new GameObject("ChildrenComponents");
        childrenComponents.transform.parent = transform;
        var directionalForce = childrenComponents.AddComponent<DirectionalForceVolume>();
        directionalForce._fieldDirection = new Vector3(0, 0, -1f);
        directionalForce.SetFieldMagnitude(10);
        childrenComponents.AddComponent<OxygenVolume>();
        initialized = true;
        Debug.Log("Finished making object");
    }

    void makeChildrenObjects()
    {
        childrenObjects = new GameObject("ChildrenObjects");
        childrenObjects.transform.parent = transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (initialized)
            return;
        initialized = true;
        // Destroy all children
        if (childrenObjects != null)
            DestroyImmediate(childrenObjects);


        // Check if children components exists
        if (childrenComponents == null)
            makeChildrenComponents();

        makeChildrenObjects();

        if (vertices.Length < 2)
            return;
        for (int i = 0; i < vertices.Length - 1; i++)
        {
            Vector3 direction = vertices[i + 1] - vertices[i];
            Vector3 between_points = Vector3.Normalize(vertices[i + 1] - vertices[i]);
            float distance_between = Vector3.Distance(vertices[i], vertices[i + 1]);
            GameObject cylinder = createVolume(width, Vector3.Distance(vertices[i], vertices[i + 1]));
            cylinder.transform.parent = childrenObjects.transform;
            cylinder.transform.localRotation = Quaternion.LookRotation(direction.normalized);
            cylinder.transform.localRotation *= Quaternion.Euler(90, 0, 0);
            cylinder.transform.localPosition = vertices[i] + (between_points * (distance_between/2.0f));
            cylinder.name = "cylinder_" + i;
        }
    }

    public void AddChildrenComponents(ref GameObject obj)
    {
        var directionalForce = obj.AddComponent<DirectionalForceVolume>();
        directionalForce._fieldDirection = new Vector3(0, 0, 1f);
        directionalForce.SetFieldMagnitude(10);
        obj.AddComponent<OxygenVolume>();
    }

    GameObject createVolume(float width, float length)
    {
        GameObject volume = new GameObject();
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        GameObject topSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject bottomSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        DestroyImmediate(cylinder.GetComponent<MeshRenderer>());
        DestroyImmediate(topSphere.GetComponent<MeshRenderer>());
        DestroyImmediate(bottomSphere.GetComponent<MeshRenderer>());

        // Add Children Components
        AddChildrenComponents(ref cylinder);
        AddChildrenComponents(ref topSphere);
        AddChildrenComponents(ref bottomSphere);

        // Add to parent
        cylinder.transform.parent = volume.transform;
        topSphere.transform.parent = volume.transform;
        bottomSphere.transform.parent = volume.transform;

        cylinder.transform.localScale = new Vector3(width, length/2.0f, width);
        topSphere.transform.localPosition = new Vector3(0, length/2.0f, 0);
        bottomSphere.transform.localPosition = new Vector3(0, -length/2.0f, 0);

        topSphere.transform.localScale = new Vector3(width, width, width);
        bottomSphere.transform.localScale = new Vector3(width, width, width);

        return volume;
    }

}