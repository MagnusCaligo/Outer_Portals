using System;
using UnityEngine;

/// <summary>
/// FaceActiveCamera faces only the active camera. we want to face any camera that's rendering
/// </summary>
public class BetterFaceActiveCamera : MonoBehaviour
{
	private bool _isMapCamActive;

	[SerializeField]
	public Vector3 _localFacingVector = Vector3.forward;

	[SerializeField]
	public Vector3 _localRotationAxis = Vector3.zero;

	[SerializeField]
	public bool _useLookAt;

	private void Awake()
	{
		OWCamera.onAnyPreCull += UpdateRotation;
		GlobalMessenger.AddListener("EnterMapView", new Callback(this.OnEnterMapView));
		GlobalMessenger.AddListener("ExitMapView", new Callback(this.OnExitMapView));
	}

	private void OnDestroy()
	{
		OWCamera.onAnyPreCull -= UpdateRotation;
		GlobalMessenger.RemoveListener("EnterMapView", new Callback(this.OnEnterMapView));
		GlobalMessenger.RemoveListener("ExitMapView", new Callback(this.OnExitMapView));
	}

	private void UpdateRotation(OWCamera cam)
	{
		if (this._isMapCamActive)
		{
			Vector3 vector = new Vector3(0f, 150000f, 0f);
			if (this._useLookAt)
			{
				base.transform.LookAt(vector);
				return;
			}
			base.transform.rotation = Quaternion.FromToRotation(base.transform.TransformDirection(this._localFacingVector), vector) * base.transform.rotation;
			return;
		}
		else
		{
			if (this._useLookAt)
			{
				base.transform.LookAt(cam.transform.position);
				return;
			}
			Vector3 vector2 = cam.transform.position - base.transform.position;
			Vector3 vector3 = vector2 - Vector3.Project(vector2, base.transform.TransformDirection(this._localRotationAxis));
			base.transform.rotation = Quaternion.FromToRotation(base.transform.TransformDirection(this._localFacingVector), vector3) * base.transform.rotation;
			return;
		}
	}

	private void OnEnterMapView()
	{
		if (this._localRotationAxis.sqrMagnitude == 0f)
		{
			this._isMapCamActive = true;
		}
	}

	private void OnExitMapView()
	{
		this._isMapCamActive = false;
	}
}
