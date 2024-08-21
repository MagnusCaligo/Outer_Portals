using NewHorizons.Utility;
using UnityEngine;

// copied from https://github.com/qsb-dev/quantum-space-buddies/blob/master/QSB/Utility/RelativeTransformUtil.cs
// since its already been tested for 2 years
public static class RelativeTransformUtil
{
	public static Vector3 ToRelPos(this Transform refTransform, Vector3 pos) =>
		refTransform.InverseTransformPoint(pos);

	public static Vector3 FromRelPos(this Transform refTransform, Vector3 relPos) =>
		refTransform.TransformPoint(relPos);

	public static Quaternion ToRelRot(this Transform refTransform, Quaternion rot) =>
		refTransform.InverseTransformRotation(rot);

	public static Quaternion FromRelRot(this Transform refTransform, Quaternion relRot) =>
		refTransform.TransformRotation(relRot);

	public static Vector3 ToRelVel(this OWRigidbody refBody, Vector3 vel, Vector3 pos) =>
		refBody.transform.InverseTransformDirection(vel - refBody.GetPointVelocity(pos));

	public static Vector3 FromRelVel(this OWRigidbody refBody, Vector3 relVel, Vector3 pos) =>
		refBody.GetPointVelocity(pos) + (relVel);

	public static Vector3 ToRelAngVel(this OWRigidbody refBody, Vector3 angVel) =>
		(angVel - refBody.GetAngularVelocity());

	public static Vector3 FromRelAngVel(this OWRigidbody refBody, Vector3 relAngVel) =>
		refBody.GetAngularVelocity() + refBody.transform.TransformDirection(relAngVel);
}
